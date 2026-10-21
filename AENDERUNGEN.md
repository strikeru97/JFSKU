# Änderungen und Grenzen

- YAML neu eingerückt; Build ohne Release möglich; Release-Build aus dem Tag.
- Schreibrechte und expliziter Push auf den Standardbranch.
- Plugin-Version wird bei Release aus vA.B.C.D abgeleitet.
- Manifest-Einträge werden ersetzt statt dupliziert und numerisch absteigend sortiert.
- Jellyfin-Pakete bleiben bei 12.2.0; targetAbi auf 12.2.0.0 angeglichen.
- Plugin erbt von BasePlugin<PluginConfiguration>.
- Klassenklammern, generische Typen, Task-Schnittstelle und Controller-Rückgabewert korrigiert.
- ILogger<FskUpdateTask> statt untypisiertem Logger.
- Speicherung über ILibraryManager.UpdateItemAsync.
- Konfigurationsseite eingebettet, API-Key-Test und Controller gemeinsam auf POST umgestellt.
- Metadatensperren, vorhandene Werte, Cancellation und leere Bibliotheken berücksichtigt.
- API-Antworten und Freigaben validiert; widersprüchliche Freigaben werden übersprungen.
- FSK-XX statt uneinheitlicher Schreibweise; Testlauf standardmäßig aktiviert.
- manifest.json verweist auf das tatsächlich mitgelieferte logo.jpg.
- README auf vorhandene Funktionen begrenzt; falsche JFSU-URL korrigiert.

Nicht verifiziert: NuGet-Restore im konkreten Repository, Kompilierung gegen die
referenzierten Assemblies, Controller-/Task-Erkennung in Jellyfin 12.2,
Webclient-Integration und Metadatenspeicherung auf einem laufenden Server.
Kein .NET-SDK in der Erstellungsumgebung. Daher keine Erfolgsgarantie und keine
Behauptung eines erfolgreichen Builds. Der Workflow ist der nächste Prüfpunkt.
