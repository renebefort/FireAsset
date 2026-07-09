# Implementierungsplan – Asset Manager (FireAsset)

Basierend auf `Spec.md`. Zielsetzung: zentrale Blazor-Webanwendung zur Verwaltung von
Geräten, Prüfungen, Aufgaben und Standorten für die Feuerwehr/Werkstatt.

## 1. Technische Basisentscheidungen

| Bereich | Entscheidung | Begründung |
|---|---|---|
| Runtime | .NET 10 (LTS) | Installiert, langfristig unterstützt |
| Frontend | Blazor Web App, Render-Mode **InteractiveServer** | Spec-Empfehlung, ≤3 Nutzer, kein Offline nötig |
| Datenbank | **SQLite** über EF Core | Eine Datei, kein DB-Server, Backup = Datei kopieren |
| ORM | Entity Framework Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`) | Migrations, saubere Entitätsmodellierung |
| UI-Library | **Radzen.Blazor** | `RadzenDataGrid`: Column-Reorder (Drag&Drop), Filter pro Spalte, Sortierung, Multiselect out-of-the-box |
| Auth | Custom Cookie-Auth + `Microsoft.AspNetCore.Identity.PasswordHasher` | Schlank, passt zur einfachen User-Tabelle der Spec |
| Export | CSV via `CsvHelper` (oder minimal StringBuilder) | Inventarliste-Export |

### Projektstruktur (ein Blazor-Server-Projekt, ordnerbasierte Schichtung)

```
FireAsset/
├─ FireAsset.sln
├─ src/FireAsset/
│  ├─ FireAsset.csproj
│  ├─ Program.cs                     # DI, Auth, DbContext, Radzen, Migrations-Apply
│  ├─ appsettings.json               # ConnectionString, Admin-Seed
│  ├─ Data/
│  │  ├─ AppDbContext.cs
│  │  ├─ Entities/                   # POCO-Entitäten
│  │  └─ Migrations/
│  ├─ Services/                      # Geschäftslogik (Task-Generierung, Versionierung, Export…)
│  ├─ Auth/                          # AuthStateProvider, Login-Logik, Seed
│  ├─ Components/
│  │  ├─ App.razor / Routes.razor
│  │  ├─ Layout/ (MainLayout, NavMenu)
│  │  └─ Pages/                      # Dashboard, Stammdaten, Artikel, Aufgaben, Protokolle
│  └─ wwwroot/
└─ IMPLEMENTATION_PLAN.md
```

> Bewusst **kein** Multi-Projekt-Setup (kein separates Domain/Infra-Assembly) — für einen
> 3-Nutzer-MVP ist ordnerbasierte Schichtung wartbarer und schneller. Services kapseln die
> Logik testbar; eine spätere Aufteilung bleibt möglich.

## 2. Datenmodell

Entitäten (Deutsch/Englisch gemischt, Code auf Englisch):

- **User** – FirstName, LastName, Email (unique), PasswordHash, IsActive, CreatedAt
- **Location** (Standort) – Name, Description, Barcode, Icon, `ParentLocationId?` (self-ref Hierarchie)
- **Category** (Kategorie) – Name, Description, IsActive
- **InspectionInterval** (Prüfintervall) – Name, IntervalMonths, `CategoryId`, `FormId`, IsActive
- **Form** (Formular) – Name, Description, IsActive, CreatedAt, `CurrentVersionId`
- **FormVersion** (Formularversion) – `FormId`, VersionNumber, CreatedAt, EditedByUserId
- **FormField** – `FormVersionId`, Label, FieldType (enum), SortOrder, ReferenceValue?, Unit?, ShowLastValue(bool)
- **Article** (Artikel) – alle Felder aus Spec (Identification, Manufacturer, Type, SerialNumber,
  ManufacturerNumber, InventoryNumber, Barcode, AcquisitionDate, ProductionDate?, DecommissionDate?,
  LegalBasis?, EndDate?, Description, `CategoryId`, `LocationId`, Status, CreatedAt/By, ModifiedAt/By,
  `CurrentInspectionStatus` = abgeleitet vom letzten Protokoll)
- **InspectionTask** (Aufgabe) – `ArticleId`, `IntervalId?` (null bei manueller Aufgabe), `FormId`,
  DueDate, Status (Neu/InBearbeitung/Erledigt), CreatedAt, IsManual
- **InspectionProtocol** (Prüfprotokoll) – `ArticleId`, `TaskId?`, `FormVersionId`, Result
  (Bestanden/Mangelhaft/NichtBestanden), Notes, CreatedAt, CreatedByUserId, IsUnplanned
- **ProtocolFieldValue** – `ProtocolId`, `FormFieldId`, Value (string, typkonvertiert)

### Enums
- `FieldType`: YesNo, SingleLineText, Integer, Decimal, MultilineText, Date
- `InspectionResult`: Bestanden, Mangelhaft, NichtBestanden
- `TaskStatus`: Neu, InBearbeitung, Erledigt

### Versionierungslogik
- Formular-Bearbeitung erzeugt neue `FormVersion` mit kopierten/aktualisierten `FormField`s;
  `Form.CurrentVersionId` zeigt auf die neueste Version.
- Protokolle speichern `FormVersionId` → alte Prüfungen bleiben exakt nachvollziehbar.

## 3. Kern-Geschäftslogik (Services)

**`TaskGenerationService`**
- Bei Artikelanlage: für jedes **aktive** Intervall der Kategorie eine Aufgabe erzeugen.
- Erstes Fälligkeitsdatum = `AcquisitionDate + IntervalMonths`.
- Validierung: wenn `DueDate > Article.EndDate` → keine Aufgabe, sammelbare Meldung zurückgeben.
- Folgeaufgabe nach Abschluss: `DueDate = CompletedDate + IntervalMonths` (gleiche Regel gegen EndDate).

**`InspectionService`**
- Aufgabe ausführen: Formular (aktuelle Version) laden, Werte erfassen, Ergebnis wählen,
  Protokoll erzeugen, Aufgabe → Erledigt, Folgeaufgabe anlegen.
- „Werte der letzten Prüfung" laden (letztes Protokoll desselben Artikels+Formulars) für Slider-Anzeige.
- Ungeplante manuelle Prüfung: Protokoll ohne Änderung/Anlage von Aufgaben (`IsUnplanned = true`).
- Artikel-`CurrentInspectionStatus` = Result des zuletzt erstellten Protokolls.

**`FormVersioningService`** – neue Version bei Formularänderung.

**`ExportService`** – CSV der Inventarliste, filterbar nach Kategorie/Standort/Status.

**`AuthService` / `AppAuthStateProvider`** – Login/Logout, Passwort-Hashing, initialer Admin-Seed.

## 4. Seiten / UI (Radzen)

- **Dashboard** – fällige Aufgaben, Prüfstatus-Übersicht, Kurzstatistiken.
- **Stammdaten**: Benutzer, Standorte (Baum-Ansicht/RadzenTree), Kategorien & Intervalle, Formulare (mit dynamischem Feld-Editor + Versionierung).
- **Artikelstamm** – RadzenDataGrid mit Spaltenfiltern, Barcode-Suchfeld, Anlegen/Bearbeiten, Standortwechsel (2× Scan).
- **Aufgabenliste** – RadzenDataGrid: verschiebbare Spalten, Filter pro Spalte, Sortierung nach Fälligkeit, Multiselect/Stapelabarbeitung, Fälligkeitsdatum editierbar, Prüfformular öffnen; manuelle Aufgabe anlegen.
- **Protokolle** – Liste (Filter Artikel/Kategorie/Status/Zeitraum) + Read-only Detailansicht.
- **Export** – Inventarliste als CSV.
- **Barcode-Workflow** – Scanner = Tastatur; Fokus-gestütztes Suchfeld leitet auf Artikel/Standort.

## 5. Meilensteine (inkrementell, jeweils lauffähig)

1. **Gerüst** – Projekt + Radzen + SQLite + Auth-Grundgerüst, Login-Seite, Admin-Seed, MainLayout/NavMenu. → `dotnet run` zeigt Login + leeres Dashboard.
2. **Datenmodell + Migration** – alle Entitäten, `AppDbContext`, erste EF-Migration, DB wird bei Start automatisch migriert.
3. **Stammdaten-CRUD** – Benutzer, Standorte (Hierarchie), Kategorien & Intervalle.
4. **Formulare + Versionierung** – dynamischer Formular-Editor, Feldtypen, Versionierung.
5. **Artikel** – Artikel-CRUD, Barcode, automatische Aufgabenanlage inkl. EndDate-Validierung, Standortwechsel per Scan.
6. **Aufgaben + Prüfung** – Aufgabenliste-Grid (Reorder/Filter/Multiselect), Prüfformular ausführen, Slider „letzte Werte", Protokoll + Folgeaufgabe, ungeplante Prüfung.
7. **Protokolle** – Liste + Read-only Detail; Prüfstatus am Artikel.
8. **Dashboard + Export** – Statistiken, fällige Aufgaben, CSV-Export.
9. **Feinschliff** – Validierung, Fehlermeldungen, HTTPS-Hinweis, README/Betriebsdoku.

## 6. Sicherheit / Betrieb
- Passwörter gehasht (`PasswordHasher<User>`), Admin initial via `appsettings`/Erststart.
- HTTPS-Redirect aktiviert (Hinweis für Produktion).
- Backup = SQLite-Datei sichern (dokumentiert).
- Keine Notifications, keine Mehrsprachigkeit, kein Offline (per Spec).

## 7. Offene fachliche Punkte (nicht blockierend für MVP)
Aus Spec „Offene Punkte für die Gerätewarte" – werden über die freie Konfigurierbarkeit
(Kategorien/Intervalle/Formulare) abgedeckt; konkrete Standard-Kategorien und -Formularfelder
können später als optionale Seed-Daten ergänzt werden.
