# ☀️ SUN GOD AI für EDOPro

Eine Computer-KI für **EDOPro (Project Ignis)**, die das Deck **„Sun God“** (The Winged Dragon of Ra) spielt – gratis, mit offenem Quellcode.
Du kannst gegen sie spielen oder sie gegen andere KIs antreten lassen.

📺 Die KI in Aktion: YouTube **[Keto Saiba](https://www.youtube.com/@KetoSaibaYGO)** · Instagram **[@ketosaiba.ygo](https://www.instagram.com/ketosaiba.ygo)** · Patreon **[Keto Saiba](https://www.patreon.com/KetoSaibaYgo)**

> *English: A free, open-source WindBot AI for EDOPro that plays the "Sun God" (Winged Dragon of Ra) deck. Download the ZIP under **Releases**, copy the folders into your ProjectIgnis folder and add the bot entry (see `ANLEITUNG.txt` in the ZIP).*

## ⬇️ Download
Rechts unter **Releases** die neueste `SunGod-AI-EDOPro-….zip` herunterladen. Die Anleitung (`ANLEITUNG.txt`) liegt im ZIP – kurz:

1. EDOPro einmal starten (Updates laden lassen), dann schließen.
2. ZIP: Rechtsklick → Eigenschaften → „Zulassen“ anhaken (falls vorhanden) → entpacken.
3. Den Inhalt von `In-ProjectIgnis-kopieren` in deinen `ProjectIgnis`-Ordner ziehen.
4. Den Text aus `bots-json-Eintrag.txt` in `ProjectIgnis\WindBot\bots.json` direkt unter die erste Zeile `[` einfügen.
5. In EDOPro einen Raum erstellen → „KI hinzufügen“ → **SUN GOD AI**.

## 🧠 Was die KI kann
- Bringt Ra in den Friedhof und beschwört **The Sun God of Darkness** mit „The Immortal Sun God“
- Leert mehrere gegnerische Zonen auf einmal und sucht den OTK (LP auf 1 → direkter Schaden)
- Erster Zug: Link-Aufbau mit Wee Witch's Apprentice und Silhouhatte Rabbit
- Als Zweiter: reagiert gezielt auf gegnerische Unterbrechungen
- Getestet in tausenden Simulator-Duellen gegen andere KIs

## 🛠️ Selbst bauen (für Programmierer)
- Quellcode: `src/SunGodExecutor.cs` (C#, .NET Framework 4.8)
- `ExecutorBase.dll` aus deinem `ProjectIgnis\WindBot`-Ordner nach `src/lib/` kopieren
- `dotnet build -c Release` in `src/` → `SunGodAI.dll` nach `ProjectIgnis\WindBot\Executors`
- Decks: `deck/AI_SunGod.ydk` (Deck der KI), `deck/SUN GOD v2.ydk` (zum Selberspielen)

Version: 2026.10.20

## ❤️ Unterstützen
Die KI ist und bleibt **kostenlos**. Wenn du die Arbeit trotzdem unterstützen möchtest: auf **[Patreon](https://www.patreon.com/KetoSaibaYgo)** (oder über den **Sponsor**-Button oben) – ganz freiwillig, du bekommst dafür keine zusätzlichen Inhalte an der KI.

## ⚖️ Lizenz & Hinweise
- Lizenz: **GNU AGPL-3.0** (siehe `LICENSE`). Die KI baut auf [WindBot Ignite](https://github.com/ProjectIgnis/windbot) von Project Ignis auf, das ebenfalls unter der AGPL-3.0 steht. Du darfst den Code nutzen, ändern und weitergeben, solange du deine Änderungen ebenfalls unter der AGPL-3.0 mit Quellcode veröffentlichst.
- **Inoffizielles Fan-Projekt.** Keine Verbindung zu Konami, Shueisha, Studio Dice oder Project Ignis. „Yu-Gi-Oh!“ sowie alle Karten- und Monsternamen sind Marken bzw. Eigentum ihrer jeweiligen Rechteinhaber. Dieses Projekt enthält keine Kartenbilder, Kartentexte oder Spieldateien – nur die KI-Logik und Decklisten.
- Ohne Gewähr: Nutzung auf eigene Verantwortung.
