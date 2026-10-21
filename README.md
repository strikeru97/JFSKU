# JFSKU

Korrigierter Quellstand für Jellyfin 12.2.0 / .NET 10.

## Prüfstatus

Diese Dateien wurden statisch überarbeitet. In der Erstellungsumgebung war kein
.NET-SDK verfügbar. Es wurde kein erfolgreicher Build und kein Jellyfin-Laufzeittest
durchgeführt. Dies ist ein Quellcodepaket, kein installierbares Plugin-Release.
Die tatsächliche Serverversion vor dem Einsatz prüfen. Keine Freigabe für 10.11.

## Repository ersetzen

Die Dateien aus diesem Archiv mit den angegebenen Pfaden ins Repository übernehmen.
Alte Kopien von Plugin.cs, PluginConfiguration.cs, FskUpdateTask.cs und
JFSKUTestController.cs an anderen Pfaden entfernen. Insbesondere keine hochgeladenen
Dateien mit Suffixen wie -2.cs oder -6.cs zusätzlich liegen lassen: Das SDK kompiliert
standardmäßig alle C#-Dateien im Projektverzeichnis und seinen Unterverzeichnissen.
Die ursprüngliche Projektdatei durch JFSKU.csproj ersetzen, nicht eine zweite behalten.
LICENSE und vorhandene Copyright-Hinweise beibehalten.

## Build

.NET 10 SDK installieren. Im Repository-Hauptverzeichnis:

```sh
dotnet restore JFSKU.csproj
dotnet build JFSKU.csproj --configuration Release
```

Der GitHub-Workflow baut bei Push auf main, Pull Requests und manuellem Start.
Ein erfolgreicher Build liefert zusätzlich das Actions-Artefakt JFSKU-build.
Ein Build allein bestätigt keine korrekte Laufzeitintegration.

## Veröffentlichung

1. Erst einen erfolgreichen Build und einen Test auf einer gesicherten Jellyfin-Instanz durchführen.
2. Einen neuen Tag wie v1.0.0.0 auf dem korrigierten Commit veröffentlichen.
3. Einen GitHub-Release für diesen Tag veröffentlichen.
4. Der Workflow lädt JFSKU_v1.0.0.0.zip hoch und aktualisiert manifest.json.
5. Der Standardbranch muss Workflow-Pushes erlauben. Bei Branchschutz die Manifest-Änderung
   über einen Pull Request übernehmen oder die Repository-Regeln passend konfigurieren.

Das Manifest verwendet targetAbi 12.2.0.0, passend zur gewählten Paketbasis.
Bei Wechsel der Jellyfin-Version Projektdatei und Workflow gemeinsam anpassen.
Bis zum ersten Release bleibt versions absichtlich leer.

Repository-Adresse für Jellyfin:

```text
https://raw.githubusercontent.com/strikeru97/JFSKU/main/manifest.json
```

Die URL setzt einen main-Branch voraus.

## Testinstallation

1. Jellyfin-Konfiguration und Datenbank sichern, anschließend Jellyfin stoppen.
2. Eine alte JFSKU-Installation entfernen oder außerhalb des Pluginordners sichern.
3. JFSKU.dll aus dem erfolgreichen Build in einen eigenen Unterordner des tatsächlichen
   Jellyfin-Pluginverzeichnisses kopieren, z.B. JFSKU_1.0.0.0.
4. Jellyfin starten und Serverlogs auf Ladefehler prüfen.
5. JFSKU-Konfiguration öffnen, TMDb API-Key eintragen, testen und speichern.
6. Testlauf eingeschaltet lassen. Aufgabe unter Geplante Aufgaben manuell starten.
7. Logs kontrollieren. Erst danach Testlauf deaktivieren und erneut starten.

## Tatsächlicher Funktionsumfang dieses Quellstands

- Filme und Serien mit vorhandener TMDb-ID abfragen.
- Nur deutsche TMDb-Werte 0, 6, 12, 16 und 18 übernehmen.
- Speicherung im Format FSK-0, FSK-6, FSK-12, FSK-16 oder FSK-18.
- Testlauf ist standardmäßig aktiv und speichert keine Metadaten.
- Ohne Überschreiboption bleiben sämtliche vorhandenen Altersfreigaben erhalten.
- Global gesperrte Einträge und gesperrtes OfficialRating werden übersprungen.
- Bei mehreren verschiedenen deutschen Freigaben wird der Eintrag übersprungen:
  Ohne sichere Schnittfassungszuordnung keine automatische Auswahl.
- Keine automatischen Standardtrigger. Zeitplan nach dem Test im Dashboard festlegen.
- Kein OMDb-Fallback und kein automatischer Bibliotheksereignis-Handler.
- Keine Umrechnung ausländischer Freigaben in deutsche Freigaben.

TMDb-Metadaten können unvollständig sein oder zur falschen Schnittfassung gehören.
Dieses Plugin ist kein offizieller FSK-Dienst und keine garantierte Jugendschutzprüfung.
Metadatenspeicher anderer Plugins können bei einer Speicherung ebenfalls ausgelöst werden.
Die Deinstallation stellt frühere Metadaten nicht wieder her.

## API-Key und Rechte

Der Key-Test benötigt Administratorrechte und sendet den Key im POST-Body an den Server,
nicht im Browser-Querystring. Der Server verwendet den Key für TMDb-Anfragen.
Die Konfiguration enthält den Key weiterhin im Klartext auf dem Server: Zugriffsrechte
und Backups schützen. Proxys und HTTP-Diagnoselogs dürfen keine Zugangsdaten veröffentlichen.

## Attribution und Lizenz

This product uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise
approved by TMDB.

Die ursprüngliche LICENSE-Datei ist unverändert enthalten. Vor öffentlicher Veröffentlichung
zusätzlich die jeweils geltenden TMDb-Attributionsanforderungen einschließlich Logo prüfen.
Dies ist ein unabhängiges Community-Projekt.
