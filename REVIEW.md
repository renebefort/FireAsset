# FireAsset – Projekt-Review (Stand 2026-07-09, Release 0.0.1)

Umfassendes Code-Review über Datenschicht, Service-Schicht und UI/Authentifizierung.
Referenzen im Format `Datei:Zeile`. Einstufung: **Kritisch → Hoch → Mittel → Gering/Hinweis**.

> **Umsetzungsstand (2026-07-10, Branch `dev`):** Alle Punkte wurden behoben – mit zwei
> bewussten Ausnahmen nach Produktentscheidung:
> **K1** (Rollenmodell) entfällt: alle Benutzer sind gleichberechtigt (Admin), und
> **H1** (Admin-Seed-Fallback-Passwort) bleibt wie dokumentiert bestehen.
> H6 (Login-Drosselung) wurde unabhängig davon umgesetzt.
> Bewusst offen gelassen (Hinweis-Ebene): Paging der Listen, Zusammenfassen der
> Dashboard-COUNTs, Barcode-Vorrangregel Artikel vs. Standort.
> Zeilenangaben unten beziehen sich auf den Review-Stand (Commit `9fd090a`).

## Gesamtbewertung

FireAsset ist für ein 0.0.1-Release **deutlich über Prototyp-Niveau**: Die schwierigen
Blazor-Server-Grundlagen sind richtig gelöst (kurzlebige `IDbContextFactory`-Kontexte,
statisch gerendertes Login für Cookie-Auth, konsequentes Razor-Encoding ohne XSS-Fläche,
`PasswordHasher` mit Rehash-on-Verify, parametrisierte EF-Queries, Formular-Versionierung
zur Protokoll-Unveränderlichkeit, Migrationen konsistent zum Modell).

Die zwei **strukturellen Hauptschwächen**:

1. **Kein Berechtigungsmodell** – es gibt nur „angemeldet / nicht angemeldet“. Jeder
   Benutzer ist faktisch Administrator (kann Benutzer verwalten, Passwörter zurücksetzen,
   alles löschen). Für ein compliance-nahes Brandschutz-Werkzeug ist das die wichtigste
   fehlende Funktion.
2. **Lösch- und Konsistenz-Story** – Cascade-Deletes vernichten Prüfhistorie, mehrstufige
   Schreibvorgänge laufen ohne Transaktion, Doppel-Submits werden nicht abgefangen. In
   einer Domäne, in der Prüfprotokolle Nachweischarakter haben, sind das echte Risiken.

Build-Status: **0 Fehler**. Warnungen: 3× CS8602 (mögliche Null-Dereferenzierung in
`Artikel.razor:32`, `Protokolle.razor:14`, `Benutzer.razor:18`), 1× BL0008
(`Login.razor:43`), NU1903 (bekannte Advisory `SQLitePCLRaw.lib.e_sqlite3`, im README
dokumentiert).

---

## Kritisch

### K1 – Kein Rollen-/Berechtigungsmodell
`Data/Entities/User.cs`, `Program.cs:48`, `Components/Pages/Stammdaten/Benutzer.razor`

Es existiert keine Rolle (`IsAdmin` o. ä.), kein Rollen-Claim beim Sign-in, kein
`[Authorize(Roles=...)]`. Jeder angemeldete Benutzer kann unter `/stammdaten/benutzer`
das Admin-Passwort zurücksetzen (`UserService.UpdateAsync` prüft den Aufrufer nicht),
Konten löschen und sämtliche Stammdaten/Artikel/Formulare zerstören. **Vollständige
Übernahme durch das niedrigst-privilegierte Konto möglich.**

### K2 – Doppelabschluss von Prüfaufgaben
`Services/InspectionService.cs:63-93`, `Components/Pages/Aufgaben/InspectionDialog.razor:182-212`

`ExecuteTaskAsync` prüft nicht, ob die Aufgabe bereits `Erledigt` ist; der
Speichern-Button hat keinen Busy-/Disabled-Schutz. Doppelklick oder zwei gleichzeitige
Bearbeiter erzeugen **zwei Protokolle für eine Aufgabe und doppelte Folgeaufgaben** –
der Prüfplan wird dauerhaft dupliziert.

---

## Hoch

### H1 – Fest hinterlegtes Standard-Admin-Passwort als stiller Fallback
`Data/DbInitializer.cs:26-27`, `appsettings.json:12-17`

Fehlt `AdminSeed:Password` in der Konfiguration, wird stillschweigend das im Repo
bekannte `ChangeMe!123` verwendet; es gibt keinen Passwort-Änderungszwang beim ersten
Login. Jede Installation, bei der das Überschreiben vergessen wird, ist mit öffentlich
bekannten Zugangsdaten erreichbar. Empfehlung: ohne konfiguriertes Passwort **Start
verweigern** oder ein Zufallspasswort generieren und einmalig loggen.

### H2 – Cascade-Delete vernichtet Prüfhistorie
`Data/AppDbContext.cs:145-148, 164-167`

Das Löschen eines Artikels löscht kaskadiert alle `InspectionTasks`, `InspectionProtocols`
und `ProtocolFieldValues`. Prüfprotokolle sind Nachweisdokumente – ein versehentliches
Löschen vernichtet die gesamte Historie unwiderruflich, obwohl `Article.IsActive` als
Soft-Delete bereits existiert. Empfehlung: Hard-Delete nur ohne Protokolle zulassen
(Restrict), sonst nur Deaktivieren.

### H3 – Prüfer-Identität geht beim Benutzer-Löschen verloren
`Data/AppDbContext.cs:176-179`, `Services/UserService.cs:105-108`

`InspectionProtocol.CreatedByUserId` ist `SetNull` beim Löschen des Benutzers, der
Prüfername wird nicht auf dem Protokoll gesnapshottet. Das Löschen eines ausgeschiedenen
Mitarbeiters anonymisiert dauerhaft, **wer** vergangene Prüfungen durchgeführt hat –
genau die Information, die ein Audit braucht. Empfehlung: Benutzer nur deaktivieren
(kein Hard-Delete) oder Namen im Protokoll denormalisieren.

### H4 – Sessions werden nach Deaktivierung/Löschung nicht beendet
`Program.cs:38-47`

Kein `OnValidatePrincipal`/Security-Stamp; mit `SlidingExpiration = true` verlängert sich
das Cookie bei jeder Anfrage. Ein deaktivierter oder gelöschter Benutzer behält mit
offenem Browser **unbegrenzt vollen Zugriff**.

### H5 – Open Redirect über `returnUrl`
`Components/Pages/Login.razor:71`

`ReturnUrl` wird nur mit `TrimStart('/')` bereinigt, nicht als lokale URL validiert.
`/login?returnUrl=%5C%5Cevil.com` führt nach erfolgreichem Login auf eine Angreiferseite
(Browser normalisieren `\\` zu `//` → protokollrelative URL) – Phishing nach echtem
Login. Empfehlung: `Url.IsLocalUrl`-Äquivalent bzw. nur relative Pfade ohne `\` zulassen.

### H6 – Kein Brute-Force-Schutz am Login
`Components/Pages/Login.razor:50-57`, `Services/UserService.cs:21-46`

Keine Sperr-/Drossel-Logik, keine Verzögerung; zusammen mit der bekannten Admin-E-Mail
`admin@fireasset.local` und fehlender Passwort-Richtlinie (siehe M6) sind Konten per
Brute-Force angreifbar. Der frühe Return bei unbekanntem Benutzer erlaubt zusätzlich
Timing-basiertes Konto-Enumerieren.

### H7 – Mehrstufige Schreibvorgänge ohne Transaktion
`Services/ArticleService.cs:60-73`, `Services/InspectionService.cs:66-92`, `Services/FormService.cs:85-124`, `InspectionDialog.razor:199-204`

Artikel anlegen + Erstaufgaben, Aufgabe abschließen + Protokoll + Folgeaufgabe sowie
Formular anlegen (3× `SaveChangesAsync`) laufen jeweils über mehrere DbContexte ohne
umspannende Transaktion. Ein Fehler dazwischen hinterlässt z. B. einen Artikel ohne
Prüfaufgaben oder eine erledigte Aufgabe **ohne Folgeaufgabe** – die Prüfkette reißt
still ab. Der Dialog schreibt zudem das Fälligkeitsdatum vor dem Abschluss (nicht atomar).

### H8 – Kategoriewechsel eines Artikels erzeugt/entfernt keine Aufgaben
`Services/ArticleService.cs:75-101`, `Services/CategoryService.cs:85-90`

Wird die Kategorie eines Artikels geändert (oder ein neues Intervall zu einer bestehenden
Kategorie angelegt), werden offene Aufgaben weder angepasst noch neue erzeugt. Ein von
„Feuerlöscher“ nach „Atemschutz“ umgehängter Artikel wird weiter nach den alten
Intervallen geprüft – **Compliance-Lücke**.

### H9 – Kein Optimistic-Concurrency-Schutz
`Data/AppDbContext.cs` (gesamtes Modell)

Kein `RowVersion`/Concurrency-Token; bei gleichzeitiger Bearbeitung desselben Datensatzes
gewinnt stillschweigend der letzte Schreiber, Änderungen des ersten gehen verloren.

### H10 – CSRF-barer Logout
`Program.cs:77-81`, `Components/Layout/MainLayout.razor:16-19`

`/logout` ist POST mit `.DisableAntiforgery()`; jede fremde Seite kann Benutzer per
Auto-Submit-Formular wiederholt abmelden. Ursache: dem Logout-Formular im interaktiven
Layout fehlt ein `<AntiforgeryToken />` – Token ergänzen statt Antiforgery abschalten.

---

## Mittel

- **M1 – CSV-Formel-Injection**: `Services/ExportService.cs:70-77` escapet Anführungszeichen,
  aber keine führenden `=`, `+`, `-`, `@`, Tab/CR. Ein Artikelname wie
  `=HYPERLINK(...)` wird beim Öffnen der `inventarliste.csv` in Excel ausgeführt.
- **M2 – Check-then-Insert-Races bei Eindeutigkeit**: E-Mail/Barcode/Versionsnummer werden
  per Vorab-Abfrage geprüft und ohne try/catch gespeichert (`UserService`,
  `ArticleService.cs:51-57`, `FormService.cs:117-124`, Dialoge). Paralleles Speichern
  endet in einer unbehandelten `DbUpdateException` → Circuit-Fehlerseite statt Meldung.
- **M3 – E-Mail-Eindeutigkeit case-sensitiv, Abfragen case-insensitiv**:
  `AppDbContext.cs:37` vs. `UserService.cs:26` – `ToLower()`-Vergleich umgeht den
  Unique-Index (Full-Scan) und `Admin@x.de`/`admin@x.de` können auf DB-Ebene koexistieren.
  Fix: `COLLATE NOCASE` auf der Spalte.
- **M4 – Gemischte Zeitbasen**: Audit-Felder `DateTime.UtcNow`, Fälligkeits-/Erledigt-Logik
  `DateTime.Today`/`Now` (lokal) – u. a. `TaskGenerationService`, `DashboardService.cs:28,49`,
  `InspectionDialog.razor:203`. SQLite speichert ohne Offset; bei UTC-Hosting oder
  TZ-Wechsel verrutschen Tagesgrenzen.
- **M5 – FK-Restrict-Fälle nicht abgefangen**: `FormService.DeleteAsync` (`FormService.cs:131-153`)
  prüft Intervalle/Protokolle, aber nicht `InspectionTasks.FormId` (Restrict,
  `AppDbContext.cs:153-156`); Löschen eines nur von offenen Aufgaben genutzten Formulars
  wirft eine rohe Exception. Gleiches Muster bei `Form`/`FormVersion` (`AppDbContext.cs:87-90, 172-175`).
- **M6 – Keine Passwort-Richtlinie**: `UserEditDialog.razor:74-78` akzeptiert 1-Zeichen-Passwörter.
- **M7 – Selbstschutz nur in der UI**: `Benutzer.razor:66-72` verhindert Selbst-Löschen
  clientseitig; `UserService.DeleteAsync` hat keinen Server-Guard, letzter aktiver
  Benutzer/eigenes Konto kann deaktiviert werden → Aussperrung der Instanz.
- **M8 – Interval-Deaktivierung beendet Prüfkette stumm**: `TaskGenerationService.cs:84-87`
  liefert `null` ohne Meldung; zusätzlich setzt Form-Löschung `InspectionInterval.FormId`
  auf `NULL` (`AppDbContext.cs:68-71`) und stoppt die automatische Aufgabenanlage lautlos.
- **M9 – Deaktivierte Artikel bleiben im Aufgaben-/Dashboard-Bestand**:
  `ArticleService.cs:97` + `DashboardService.cs:31-38` – ein stillgelegter Feuerlöscher
  bläht dauerhaft die Überfällig-Zahlen auf; Task-Generierung ignoriert `IsActive`.
- **M10 – Client-Eingaben beim Prüfabschluss unvalidiert**: `InspectionService.cs:63-93` –
  `formVersionId`/`FormFieldId`s werden nicht gegen das Formular der Aufgabe geprüft;
  `completedDate` wird für die Folgeplanung genutzt, aber nicht am Protokoll gespeichert
  und nicht plausibilisiert (`InspectionService.cs:64,81`).
- **M11 – SQLite ohne WAL/Busy-Timeout**: `Program.cs:22` – unter parallelen Circuits
  drohen sporadische „database is locked“-Fehler.
- **M12 – Seed/Migration nicht multi-instanzfähig**: `DbInitializer.cs:19-24` –
  `MigrateAsync` + `AnyAsync` + Insert ist nicht atomar; zwei Instanzen beim Erststart
  kollidieren (Unique-Index bzw. DB-Lock).
- **M13 – Cookie-Härtung unvollständig**: `Program.cs:39-47` – kein
  `SecurePolicy = Always`, kein explizites `SameSite`.
- **M14 – `Down()`-Migration fehlerhaft**: `20260709190532_IntervalFormOptional.cs:41-49` –
  Rollback setzt `FormId NOT NULL DEFAULT 0`; legitime NULL-Zeilen werden zu ungültigen
  FK-Referenzen, Rollback scheitert auf realen Daten.
- **M15 – Kategorienamen nicht eindeutig**: `AppDbContext.cs:54-59` – zwei Kategorien
  „Feuerlöscher“ möglich → mehrdeutige Auswahllisten, gesplittete Intervalle.
- **M16 – Kein `(ProtocolId, FormFieldId)`-Unique**: `AppDbContext.cs:182-193` –
  Doppel-Submit kann zwei widersprüchliche Werte für dasselbe Feld speichern.

---

## Gering / Hinweise

- Listen ohne Paging: `ProtocolService.cs:32-52`, `TaskService.cs:36-69`,
  `ArticleService.cs:21-29` laden ganze Tabellen inkl. Includes – bei einigen Jahren
  Historie Speicher-/Latenzproblem im Circuit.
- `DashboardService.GetStatsAsync` (`:33-43`): 9 sequentielle COUNT-Roundtrips, wäre in
  einer gruppierten Abfrage machbar (bei aktueller Größe unkritisch).
- Messwerte als kulturformatierte Strings: `ProtocolFieldValue.cs:19` – „1,5“ vs. „1.5“
  macht historische Dezimal-/Datumswerte später schwer auswertbar; Invariant-Culture
  beim Serialisieren erzwingen.
- Barcode-Prüfungen inkonsistent: `LocationService.cs:44-49` vergleicht ungetrimmt,
  gespeichert wird getrimmt; Leerstring-Barcodes (`""`) fallen nicht unter den
  Filtered-Unique-Index (`AppDbContext.cs:47,122`); Standort- und Artikel-Barcodes
  können identisch sein (Scan-Mehrdeutigkeit ohne Vorrangregel).
- Zyklenschutz für Standorte nur in der UI: `LocationService.cs:59-72` – `UpdateAsync`
  ruft `WouldCreateCycleAsync` nicht selbst auf; DB erlaubt sogar Selbstreferenz.
- `ChangeLocationByBarcodeAsync` setzt `ModifiedByUserId` nicht (Audit-Lücke) und
  wirft bei `null`-Eingabe `NullReferenceException` (`ArticleService.cs:122-141`).
- Denormalisierter `Article.CurrentInspectionStatus` (`Article.cs:50`) kann bei Schreib-
  pfaden außerhalb des Service driften.
- Aufgabe referenziert `Form` (veränderlicher Kopf), Protokoll `FormVersion`
  (`InspectionTask.cs:19`): Formular mit `CurrentVersionId = NULL` macht Aufgaben
  unabschließbar – App-seitiger Guard fehlt.
- Enum `TaskStatus` (`Enums.cs:34`) kollidiert mit `System.Threading.Tasks.TaskStatus`.
- `/not-found` ohne `[Authorize]` mit `MainLayout`: anonyme Besucher sehen die
  Navigationsstruktur (`NotFound.razor:1-8`).
- Kategorien-Löschwarnung (`Kategorien.razor:99`) erwähnt referenzierende
  Artikel/Aufgaben nicht.
- Kosmetik: ungenutzte `@ref="_grid"`-Felder (`Benutzer.razor:39`, `Kategorien.razor:67`),
  gemischte Anführungszeichen `„…"` (`Kategorien.razor:40`, `Artikel.razor:101`),
  SQL-Server-Bracket-Syntax `[Barcode]` im SQLite-Index-Filter, Compiler-Warnungen
  CS8602 (3×) und BL0008 (`Login.razor:43`).
- Login-Rehash bei parallelen Logins desselben Benutzers ist ein harmloser
  Last-Write-Wins (`UserService.cs:39-43`).

---

## Positiv hervorzuheben

- Statisch gerendertes Login für Cookie-Sign-in korrekt umgesetzt (`App.razor:29-34`,
  `RedirectToLogin.razor` mit `forceLoad: true`).
- Keine XSS-Fläche: kein `MarkupString`/Raw-HTML, konsequentes Razor-Encoding.
- Durchgängig korrektes `IDbContextFactory`-Muster, keine geteilten Kontexte über awaits.
- `PasswordHasher<User>` (PBKDF2) mit Rehash-on-Verify; EF-Queries parametrisiert.
- Formular-Versionierung hält archivierte Protokolle strukturell stabil.
- Migrationen und `ModelSnapshot` ohne Drift zum Fluent-Modell.
- Alle routbaren Seiten tragen `[Authorize]`; `/export` ist abgesichert.

---

## Empfohlene Reihenfolge

1. **K1** Rollenmodell (Admin vs. Benutzer) einführen und serverseitig durchsetzen.
2. **K2 + H7** Aufgabenabschluss idempotent machen (Status-Guard) und mehrstufige
   Schreibvorgänge in Transaktionen fassen; Doppel-Submit-Guards in Dialogen.
3. **H1 + H6** Admin-Seed-Fallback entfernen, Login-Drosselung/Lockout ergänzen.
4. **H2 + H3** Löschverhalten auf Soft-Delete umstellen (Artikel mit Protokollen,
   Benutzer generell), Prüfernamen am Protokoll snapshotten.
5. **H4, H5, H10** Session-Revalidierung, `returnUrl`-Validierung, Logout-Antiforgery.
6. **H8, H9** Aufgaben-Neuberechnung bei Kategoriewechsel, `RowVersion` einführen.
7. Danach die Mittel-Punkte (CSV-Injection, `COLLATE NOCASE`, WAL, Zeitbasis,
   FK-Fehlerbehandlung, Passwort-Richtlinie) gesammelt abarbeiten.
