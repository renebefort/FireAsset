# Design: Dokumente (freier Brief & Verwendungsnachweis)

**Datum:** 2026-07-15
**Status:** Freigegeben (Design), Implementierung ausstehend
**Kontext:** Für zwei Prüf-Anwendungsfälle mit externen Prüfern werden erzeugbare,
speicherbare und als PDF exportierbare Dokumente benötigt.

## Ziel

Zwei Dokumenttypen abbilden:

1. **Freier Brief** – Abschnitte Titel, Empfänger, Absender, Betreff, Hauptteil,
   Signatur. Folgt einer Vorlage, immer frei anpassbar. A4-Hochformat.
2. **Verwendungsnachweis** – feste Struktur: Titel, Empfänger, Absender, Art der
   Verwendung (nach Einsatz / nach Übung / zur Regelwartung), Verwendungszweck + Ort,
   Verwendungsdatum, Auftragsdatum, Liste erfasster Artikel gruppiert nach Kategorie mit
   Summenzeile je Gruppe (Artikelnummern = **Barcode**, kommagetrennt), Bemerkungsfeld,
   Signatur.

Der Verwendungsnachweis löst beim Abschluss echte Bestandsänderungen aus (Umbuchung auf
einen Zielstandort, Stilllegung von FTZ-Pool-Geräten).

## Getroffene Entscheidungen

| Frage | Entscheidung |
|---|---|
| Lebenszyklus eines Dokuments | **Entwurf → Abgeschlossen.** Entwurf frei editierbar, keine Nebenwirkungen. Abschluss führt Nebenwirkungen genau einmal aus und sperrt das Dokument. Gilt für **beide** Typen. |
| FTZ-Pool: offene Aufgaben | Auf Status **`Stillgelegt`** setzen (nicht löschen) — konsistent mit bestehendem `FinalizePoolDeviceAsync` / manuellem Stilllegen; Historie bleibt erhalten. |
| „Artikelnummer" im Verwendungsnachweis | **Barcode** des Artikels (immer vorhanden, da Erfassung per Barcode). |
| Zielstandort | **Vorlage-Default (Typ 2) + pro Dokument änderbar** per Dropdown. |
| Abgeschlossene Dokumente | **Schreibgeschützt, kein Storno.** Nur Entwürfe löschbar. Korrektur manuell (z. B. neuer Standortwechsel / Artikel reaktivieren). |
| Gemischte Artikelliste | **Erlaubt.** Pool-Geräte werden stillgelegt, Nicht-Pool nur umgebucht. Abschluss-Dialog zeigt Zähler zur Bestätigung. |
| Datenmodell | **Ansatz A** — ein relationales `Document`-Entity mit nullable Typ-2-Feldern + Kindtabelle `DocumentArticle` mit Snapshot-Spalten. |
| Zugriff | Vorlagen-Pflege unter Stammdaten wie Standorte/Formulare (alle angemeldeten Benutzer, kein Admin-Zwang). Dokumente anlegen/abschließen für alle angemeldeten Benutzer. |
| Mobile | **Der Verwendungsnachweis muss auf Tablet und Smartphone erfassbar sein.** Der Editor folgt dem mobil optimierten Muster der Schnellaktionen (`StandortWechsel.razor`), nicht einem breiten Desktop-Dialog. |

## Architektur — Ansatz A (relational)

Begründung: passt zum durchgängig relationalen Modell des Projekts (keine JSON-Blobs),
ein gemeinsames „Dokumente"-Grid ist eine einzige Query, echte FKs, sauber filter-/
exportierbar. Typ-2-Felder sind bei Briefen schlicht `null`.

### Neue Entities

```
DocumentTemplate                       (Stammdaten-Vorlage)
  Id, Name, Type (DocumentType), IsActive, Version
  TitleDefault?, RecipientDefault?, SenderDefault?, SubjectDefault?,
  BodyDefault?, SignatureDefault?
  DefaultTargetLocationId? → Location   (nur Typ 2 relevant)

Document                               (gespeichertes Dokument)
  Id, TemplateId?, Type, Status (DocumentStatus)
  Title, Recipient, Sender, Subject, Body, Signature
  -- Typ-2-Felder (nullable) --
  UsageKind? (UsageKind), UsagePurpose? (Verwendungszweck und Ort, ein Feld),
  UsageDate?, OrderDate?, TargetLocationId? → Location, Remarks?
  -- Audit --
  CreatedAt, CreatedByUserId?, ModifiedAt?, ModifiedByUserId?,
  CompletedAt?, CompletedByUserId?, Version

DocumentArticle                        (Artikelzeile, nur Typ 2)
  Id, DocumentId → Document (Cascade)
  ArticleId? → Article (SetNull bei Artikel-Löschung)
  BarcodeSnapshot, IdentificationSnapshot, CategoryNameSnapshot
```

### Enums

```
DocumentType   { Brief = 0, Verwendungsnachweis = 1 }
DocumentStatus { Entwurf = 0, Abgeschlossen = 1 }
UsageKind      { NachEinsatz = 0, NachUebung = 1, Regelwartung = 2 }
```

EF-Migration analog zu den bestehenden `AddXxx`-Migrationen; DbContext-Konfiguration
(FK-Verhalten, Concurrency-Token `Version`).

## Komponenten

### Menü & Navigation
- Neuer Top-Level-Menüpunkt **„Dokumente"** (Icon `mail`, Path `dokumente`).
- Unter **Stammdaten** neuer Eintrag **„Dokumentvorlagen"** (Icon `drafts`,
  Path `stammdaten/dokumentvorlagen`).

### Stammdaten: Dokumentvorlagen
- `Dokumentvorlagen.razor` (Grid: Name, Typ, aktiv) + `DocumentTemplateEditDialog.razor`
  (Muster: `Formulare.razor` / `FormEditorDialog`).
- Dialog blendet je `Type` die passenden Default-Felder ein. Typ 2 zusätzlich
  Default-Zielstandort (Location-Dropdown).
- Service: `DocumentTemplateService` (CRUD, Concurrency über `Version`).

### Dokumente-Übersicht
- `Dokumente.razor`: `RadzenDataGrid` über alle `Document` (Typ, Titel/Betreff,
  Empfänger, Status-Badge, Erstellt am/von, Abgeschlossen am). Filter Typ/Status.
- Kopf: **„Neues Dokument"** als Dropdown/SplitButton der aktiven Vorlagen → öffnet den
  passenden Editor mit vorbelegten Werten.
- Zeilenaktionen: **Öffnen** (Entwurf → Editor, Abgeschlossen → Ansicht), **PDF**
  (jederzeit), **Löschen** (nur Entwürfe).

### Editor Typ 1 – Freier Brief (`LetterEditor.razor`)
- Felder Titel, Empfänger, Absender, Betreff, Hauptteil (mehrzeilig), Signatur —
  vorbelegt aus Vorlage, frei änderbar.
- Aktionen: **Abbrechen**, **Speichern** (Entwurf), **Abschließen** / **Abschließen + PDF**
  (Brief hat keine Bestandsnebenwirkungen; Abschluss = schreibgeschützt setzen).

### Editor Typ 2 – Verwendungsnachweis (`UsageCertificateEditor.razor`)
- Kopf: Empfänger, Absender (vorbelegt), Art der Verwendung, Verwendungszweck + Ort,
  Verwendungsdatum, Auftragsdatum, Zielstandort (vorbelegt aus Vorlage), Bemerkung,
  Signatur.
- **Artikelerfassung** per Barcode — Erfassungs-UX aus `StandortWechsel.razor`
  wiederverwenden (Scanner/Tastatur, Enter, Vorschlagsliste, Dedup, Live-Liste, Entfernen).
- **Live-Gruppierungsvorschau**: nach Kategorie gruppiert, Barcodes kommagetrennt +
  Anzahl je Gruppe, Gesamtsumme.
- Aktionen: **Abbrechen**, **Speichern** (Entwurf, keine Nebenwirkungen),
  **Abschließen + PDF** (Nebenwirkungen, siehe unten).
- **Mobil-optimiert (Pflicht):** Single-Column-Layout, zentriert, `max-width` wie
  `StandortWechsel.razor`; große Touch-Eingabefelder; Barcode-Feld mit Auto-Fokus für
  Scanner; Aktionen als vollbreite, gut tappbare Buttons; die Gruppierungsvorschau
  vertikal stapelbar. Erfassung auf Smartphone/Tablet ist der primäre Anwendungsfall,
  Desktop der Sonderfall. Als eigene Seite (route), nicht als modaler Desktop-Dialog,
  damit die Ansicht die volle Viewport-Breite nutzt.

## Abschluss-Logik (kritische Transaktion)

`DocumentService.FinalizeAsync(documentId, userId, ...)` in **einer** Transaktion
(Muster: `TaskService.DecommissionAsync` mit `ExecuteUpdateAsync`-Guard gegen
Doppelabschluss):

1. Status Entwurf→Abgeschlossen atomar setzen; 0 betroffene Zeilen ⇒ „bereits abgeschlossen".
2. Nur Typ 2: Artikel-Snapshots (Barcode/Identifikation/Kategorie) einfrieren.
3. Nur Typ 2: alle Artikel der Liste → `TargetLocationId` umbuchen
   (Muster: `ChangeLocationForArticlesAsync`).
4. Nur Typ 2: je Artikel mit `IsPoolDevice == true` offene Aufgaben → `Stillgelegt`,
   danach `FinalizePoolDeviceAsync` (Artikel `IsActive=false`, `EndDate=heute`).
   Nicht-Pool-Artikel: nur Umbuchung.
5. `CompletedAt/By` setzen, `SaveChanges`, `Commit`.
6. Bei „+ PDF": PDF erzeugen und ausliefern.

Bestätigungsdialog vor Abschluss mit Zählern („X Pool-Geräte werden stillgelegt,
Y nur umgebucht").

## PDF-Erzeugung

`DocumentPdfService` (MigraDoc, A4-Hochformat) analog `ProtocolPdfExportService`.
- **Brief:** Absender-Block, Empfänger-Block, Betreff (fett), Hauptteil, Signatur.
- **Verwendungsnachweis:** Kopfdaten-Tabelle (Empfänger/Absender/Art/Zweck+Ort/
  Verwendungs-/Auftragsdatum) + Artikeltabelle gruppiert nach Kategorie mit Summenzeile
  je Gruppe und Gesamtsumme (Barcodes kommagetrennt), dann Bemerkung + Signatur.

## Fehlerbehandlung, Berechtigungen, Edge Cases

- `[Authorize]` auf allen Seiten. Vorlagen-Pflege ohne Admin-Zwang (wie Standorte).
- Typ-2-Abschluss: leere Artikelliste ⇒ Hinweis, kein Abschluss. Zielstandort Pflicht.
- Zielstandort/Artikel zwischenzeitlich gelöscht ⇒ Fehlermeldung (wie
  `ChangeLocationForArticlesAsync`). `DocumentArticle.ArticleId` = `SetNull`, Snapshots
  bleiben lesbar.
- Abgeschlossene Dokumente schreibgeschützt, kein Storno.
- Concurrency über `Version` wie bei anderen Entities.

## Nicht im Umfang (YAGNI)

- Storno / Rückabwicklung abgeschlossener Dokumente.
- Konfigurierbare „Art der Verwendung" (bleibt fester Enum).
- Rich-Text im Hauptteil (reiner mehrzeiliger Text).

## Betroffene / neue Dateien (Übersicht)

- `Data/Entities/DocumentTemplate.cs`, `Document.cs`, `DocumentArticle.cs`, `Enums.cs` (erw.)
- `Data/AppDbContext.cs` (DbSets + Konfiguration), neue Migration
- `Services/DocumentTemplateService.cs`, `DocumentService.cs`, `DocumentPdfService.cs`
- `Components/Pages/Stammdaten/Dokumentvorlagen.razor`, `DocumentTemplateEditDialog.razor`
- `Components/Pages/Dokumente/Dokumente.razor`, `LetterEditor.razor`,
  `UsageCertificateEditor.razor`
- `Components/Layout/MainLayout.razor` (Menü)
- `Program.cs` (DI-Registrierung der Services)
- PDF-Download-Endpoint (Muster: bestehender Protokoll-/Export-Download)
- `Handbuch.razor` + Changelog-Eintrag
