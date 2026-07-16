# FireAsset – Installation unter Windows 11 (für Administratoren)

Diese Anleitung beschreibt Schritt für Schritt, wie ein Administrator FireAsset auf einem
Windows-11-Rechner installiert und dauerhaft betreibt. Sie richtet sich an eine kleine
Vor-Ort-Installation (ein Rechner als Server, mehrere Clients im selben Netz greifen per
Browser zu).

> **Kurzfassung:** .NET-Runtime installieren → SQL Server bereitstellen → veröffentlichte
> App kopieren → `dbsettings.json` eintragen → einmalig starten (DB + Admin werden automatisch
> angelegt) → Admin-Passwort ändern → als Dienst dauerhaft betreiben.

---

## 1. Systemvoraussetzungen

- **Windows 11** (64-bit), möglichst als „immer eingeschalteter" Rechner.
- **ASP.NET Core Runtime 10** (nicht nur die .NET Desktop Runtime).
  - Download: <https://dotnet.microsoft.com/download/dotnet/10.0> → **ASP.NET Core Runtime → Hosting Bundle / Windows x64 Installer**.
  - Prüfen nach der Installation (PowerShell):
    ```powershell
    dotnet --list-runtimes
    ```
    Es muss eine Zeile `Microsoft.AspNetCore.App 10.x.x` enthalten sein.
- **Microsoft SQL Server** – lokal oder im Netz erreichbar. Für eine kleine Installation genügt
  **SQL Server 2022 Express** (kostenlos):
  <https://www.microsoft.com/sql-server/sql-server-downloads>.
- Optional, aber empfohlen: **SQL Server Management Studio (SSMS)** zum Anlegen des Logins und für Backups.

---

## 2. SQL Server vorbereiten

1. **SQL Server Express** installieren (Variante „Basic" reicht). Instanzname z. B. `SQLEXPRESS`.
2. **SQL-Authentifizierung aktivieren** (Mixed Mode), falls die App sich mit Benutzer/Passwort
   anmelden soll (empfohlen für einen eigenständigen Dienst):
   - In SSMS: Servereigenschaften → *Security* → **SQL Server and Windows Authentication mode** → SQL Server neu starten.
3. **Login für FireAsset anlegen** (SSMS → *Security → Logins → New Login*):
   - SQL-Login, z. B. `fireasset`, mit Passwort.
   - Serverrolle **`dbcreator`** zuweisen (damit die App die Datenbank beim ersten Start selbst anlegen darf).
     Alternativ die Datenbank `FireAsset` vorab anlegen und dem Login dort `db_owner` geben.
4. **TCP/IP aktivieren**, falls Clients/Dienst nicht über Shared Memory zugreifen:
   - *SQL Server Configuration Manager* → *SQL Server Network Configuration* → Protokolle → **TCP/IP** aktivieren → SQL Server neu starten.

> Läuft SQL Server auf demselben Rechner, ist als Server meist `localhost` oder `localhost\SQLEXPRESS` einzutragen.

---

## 3. Anwendung bereitstellen

Die Anwendung wird als **veröffentlichter Ordner** ausgeliefert (Framework-abhängig, nutzt die in
Schritt 1 installierte Runtime).

**Variante A – fertiges Paket erhalten:** Den vom Build-/Release-Prozess erzeugten `publish`-Ordner
auf den Zielrechner kopieren, z. B. nach `C:\FireAsset`.

**Variante B – selbst veröffentlichen** (auf einem Rechner mit .NET-10-SDK und dem Quellcode):
```powershell
dotnet publish src/FireAsset -c Release -o C:\FireAsset
```

Ergebnis: In `C:\FireAsset` liegen u. a. `FireAsset.dll`, `FireAsset.exe`, `appsettings.json`
und `dbsettings.example.json`.

---

## 4. Datenbank-Verbindung konfigurieren

Im Anwendungsordner (`C:\FireAsset`) die Datei **`dbsettings.json`** anlegen (Vorlage
`dbsettings.example.json` kopieren) und den Connection-String eintragen:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=FireAsset;User Id=fireasset;Password=DEIN_PASSWORT;TrustServerCertificate=True"
  }
}
```

- `TrustServerCertificate=True` ist bei einem lokalen SQL Server ohne gültiges Zertifikat nötig.
- Für Windows-Authentifizierung statt SQL-Login:
  `Server=localhost\SQLEXPRESS;Database=FireAsset;Trusted_Connection=True;TrustServerCertificate=True`
  (dann muss das **Dienstkonto** Rechte auf dem SQL Server haben).

> `dbsettings.json` enthält Zugangsdaten und ist bewusst nicht Teil des Quellcode-Repositorys.

---

## 5. Initiales Admin-Konto festlegen

Beim allerersten Start legt die App automatisch einen Administrator an – **nur solange noch kein
Benutzer existiert**. Die Standardwerte lauten:

- **E-Mail:** `admin@fireasset.local`
- **Passwort:** `ChangeMe!123`

Diese sollten vor dem ersten Start überschrieben werden – am saubersten über **Umgebungsvariablen**
(nicht im Klartext in `appsettings.json`):

```powershell
setx AdminSeed__Email "admin@deine-domain.de" /M
setx AdminSeed__Password "EinSicheresPasswort!" /M
```

(`/M` schreibt in die Systemumgebung; ein neues Terminal/Neustart übernimmt die Werte. Das doppelte
Unterstrich `__` ist die .NET-Schreibweise für verschachtelte Konfigurationsschlüssel.)

Das Passwort lässt sich später jederzeit in der App unter **Stammdaten → Benutzer** ändern.

---

## 6. Erster Start & Test

1. Adresse/Port festlegen und starten (PowerShell im Anwendungsordner):
   ```powershell
   cd C:\FireAsset
   $env:ASPNETCORE_URLS = "http://0.0.0.0:8080"
   .\FireAsset.exe
   ```
   - `0.0.0.0` macht die App auch für andere Rechner im Netz erreichbar; `8080` ist frei wählbar.
2. Beim ersten Start wird die Datenbank **FireAsset** angelegt bzw. migriert und der Admin erzeugt.
   In der Konsole erscheint `Now listening on: http://0.0.0.0:8080` und `Application started`.
3. Im Browser öffnen:
   - lokal: `http://localhost:8080`
   - von anderen Rechnern: `http://<IP-oder-Hostname-des-Servers>:8080`
4. Mit dem Admin-Konto anmelden und **sofort das Passwort ändern** (falls Standardwerte genutzt wurden).

---

## 7. Windows-Firewall freigeben

Damit Clients im Netz zugreifen können, den gewählten Port freigeben (Beispiel 8080, PowerShell als Administrator):

```powershell
New-NetFirewallRule -DisplayName "FireAsset (HTTP 8080)" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow
```

---

## 8. Dauerhaft betreiben (als Dienst)

Damit FireAsset ohne offenes Konsolenfenster und nach einem Neustart automatisch läuft, empfiehlt
sich der Betrieb als **Windows-Dienst**. Am einfachsten mit dem kostenlosen Tool **NSSM**
(<https://nssm.cc>):

```powershell
# Dienst anlegen (einmalig, als Administrator)
nssm install FireAsset "C:\FireAsset\FireAsset.exe"
nssm set FireAsset AppDirectory "C:\FireAsset"
nssm set FireAsset AppEnvironmentExtra "ASPNETCORE_URLS=http://0.0.0.0:8080" "ASPNETCORE_ENVIRONMENT=Production"
nssm set FireAsset Start SERVICE_AUTO_START

# starten / stoppen
nssm start FireAsset
nssm stop FireAsset
```

Alternativ (ohne Zusatztool) genügt für einfache Fälle eine **geplante Aufgabe** (Task Scheduler),
die `FireAsset.exe` „Beim Start des Computers" mit dem Arbeitsverzeichnis `C:\FireAsset` ausführt.

---

## 9. HTTPS (empfohlen für den Produktivbetrieb)

Die App erzwingt HTTPS-Weiterleitung und HSTS außerhalb der Entwicklung. Für verschlüsselten
Zugriff gibt es zwei übliche Wege:

- **Reverse-Proxy** (IIS mit dem *ASP.NET Core Hosting Bundle*, oder nginx) vor die App setzen und
  das Zertifikat dort terminieren – die empfohlene Variante.
- **Kestrel direkt** mit Zertifikat: `ASPNETCORE_URLS=https://0.0.0.0:8443` plus konfiguriertem
  Zertifikat (`ASPNETCORE_Kestrel__Certificates__Default__Path` / `__Password`).

Ohne Zertifikat ist der Betrieb nur innerhalb eines vertrauenswürdigen internen Netzes über `http` vertretbar.

---

## 10. Datensicherung (Backup)

Alle Anwendungsdaten liegen in der SQL-Server-Datenbank **FireAsset** (inkl. Fotos, Protokolle,
Dokumente und Logbuch). Es gibt bewusst keine anwendungsinterne Backup-Funktion – die Sicherung
erfolgt über SQL-Server-Bordmittel:

- In SSMS: Rechtsklick auf die Datenbank → *Tasks → Back Up…*, oder
- per T-SQL / Wartungsplan (bei SQL Server Express ohne Agent z. B. per geplanter Aufgabe):
  ```sql
  BACKUP DATABASE [FireAsset] TO DISK = N'C:\Backups\FireAsset.bak' WITH INIT, COMPRESSION;
  ```

Die `dbsettings.json` und ggf. gesetzte Umgebungsvariablen sollten zusätzlich dokumentiert/gesichert werden.

---

## 11. Aktualisieren auf eine neue Version

1. Dienst stoppen (`nssm stop FireAsset` bzw. geplante Aufgabe beenden).
2. Den Inhalt von `C:\FireAsset` durch die neue veröffentlichte Version ersetzen –
   **`dbsettings.json` behalten** (nicht überschreiben).
3. Dienst wieder starten. Ausstehende **Datenbank-Migrationen werden beim Start automatisch angewendet**.

> Vor größeren Updates empfiehlt sich ein vorheriges Datenbank-Backup (Abschnitt 10).

---

## 12. Fehlerbehebung

| Symptom | Ursache / Lösung |
|---|---|
| Start bricht mit „Kein Connection-String konfiguriert" ab | `dbsettings.json` fehlt oder liegt nicht im Anwendungsordner. |
| „Login failed for user …" | Falsches SQL-Passwort, SQL-Auth nicht aktiviert oder TCP/IP deaktiviert. |
| „CREATE DATABASE permission denied" | Login fehlt die Rolle `dbcreator` – zuweisen oder DB vorab anlegen. |
| Andere Rechner erreichen die App nicht | `ASPNETCORE_URLS` auf `0.0.0.0:<Port>` gesetzt? Firewall-Regel vorhanden (Abschnitt 7)? |
| Admin-Login funktioniert nicht | Der Admin wird nur angelegt, wenn die DB **keinen** Benutzer enthält. Bei bereits vorhandener DB das Passwort in *Stammdaten → Benutzer* zurücksetzen (mit einem anderen Admin) oder die `AdminSeed`-Werte greifen nur bei leerer Benutzertabelle. |

---

## Technischer Überblick (Kurzreferenz)

- **Plattform:** .NET 10, ASP.NET Core / Blazor Web App (Interactive Server).
- **Datenbank:** MS SQL Server, EF Core; Migrationen werden beim Start automatisch angewendet.
- **Kommunikation:** nach dem ersten Seitenaufruf via SignalR/WebSocket; keine öffentliche REST-API.
- **Konfiguration:** Connection-String in `dbsettings.json`; Admin-Seed und weitere Schlüssel in
  `appsettings.json` bzw. Umgebungsvariablen (`AdminSeed__Password` usw.).
