# Code2Web

**Code2Web** er et lille, hurtigt CLI-værktøj, der gennemgår elevers projektmapper og genererer en samlet HTML-visning af al deres kode.
Det gør det let for lærere, censorer og elever selv at:

- få **hurtigt overblik** over et helt projekt
- browse **alle filer i én HTML-side pr. elev**
- se kildekode med **syntax highlighting**
- undgå at åbne løsrevne filer i IDE’en
- evaluere/bedømme projekter langt mere effektivt

Værktøjet fungerer på både **Windows** og **macOS** (Intel + Apple Silicon).

---

## ✨ Features

- Gennemgår en *input-mappe* (typisk en afleveringsmappe)
- Finder relevante kodefiler (C#, Java, Python, XML, JSON, HTML m.fl.)
- Normaliserer HTML så `<script>` ikke eksekveres
- Laver én HTML-side pr. elev (folder)
- Laver en samlet *index.html* med links til alle grupper
- Syntax highlighting via Highlight.js
- Fold alle / luk alle-knapper for store filer
- Styrbar rekursionsdybde (default: **5**)
- Kan ekskludere tekniske foldere (`obj/`, `.idea/`, `.vs/`, `bin/`, `__MACOSX/`)
- Mulighed for at inkludere dem med CLI-flags (`--include-obj`, `--include-idea`, `--include-vs`, `--include-bin`, `--include-json`)

---

# 🚀 Installation

Du skal **ikke** clone repoet for at bruge værktøjet.

Gå til:

👉 **Releases → Latest**  
https://github.com/<dit-brugernavn>/Code2Web/releases/latest

Her finder du zip-filer til:

- **Windows (x64)**
- **macOS ARM (M1/M2/M3)**
- **macOS Intel (x64)**

Download den zip-fil, der passer til din maskine, og pak den ud.

---

## 🪟 Windows installation

1. Download `Code2Web-win-x64.zip`
2. Udpak filen
3. Læg `cliCode2Web.exe` i en mappe, fx:

```
C:\Users\<dig>\cli\
```

4. (Valgfrit) Tilføj mappen til PATH:

```
setx PATH "%PATH%;C:\Users\<dig>\cli"
```

5. Kør værktøjet:

```powershell
cliCode2Web --help
```

---

## 🍏 macOS installation

1. Download enten:
   - `Code2Web-macos-arm64.zip` (Apple Silicon)
   - `Code2Web-macos-x64.zip` (Intel)

2. Udpak zip-filen
3. Gør filen eksekverbar (bevares normalt af zip, men for en sikkerheds skyld):

```bash
chmod +x cliCode2Web
```

4. Flyt den til en folder, fx:

```
~/cli/
```

5. (Valgfrit) Tilføj til din PATH i `.zshrc`:

```bash
export PATH="$HOME/cli:$PATH"
```

6. Test:

```bash
cliCode2Web --help
```

---

# 🧭 Brug

Gå ind i mappen med elevafleveringer:

```
cd "C:\Users\mje\Downloads\hold\2k25"
```

Kør derefter:

```bash
cliCode2Web --class 2k25
```

Dette genererer:

```
~/Documents/Code2Web/2k25/
    index.html
    elevA.html
    elevB.html
    elevC.html
    ...
```

Åbn `index.html` i en browser.

---

# ⚙️ Options

Kør:

```bash
cliCode2Web --help
```

Typiske options:

```
--class <navn>           Sæt holdnavn (bruges til output-folder)
--depth <n>              Rekursionsdybde ved søgning efter filer (default: 5)
--include-obj            Medtag obj-mapper
--include-bin            Medtag bin-mapper
--include-idea           Medtag .idea-mapper (Rider)
--include-vs             Medtag .vs-mapper (Visual Studio)
--include-json           Medtag .json-filer som ellers filtreres
--output <folder>        Overstyr default output-folder
```

Default-output er:

```
Windows: C:\Users\<dig>\Documents\Code2Web\<class>\
macOS:   /Users/<dig>/Documents/Code2Web/<class>/
```

Input er altid den **mappe du står i**.

---

# 🔍 Interested in the source code?

Hele værktøjet er open source og skrevet i **C# / .NET 8**.

## Requirements
- Visual Studio 2022 (Windows)  
  eller  
- Rider (macOS eller Windows)

## Build
```
dotnet build
```

## Publish (Windows)
```
publish-win.ps1
```

## Publish (macOS ARM)
```
./publish-mac.sh
```

## Publish (macOS Intel)
```
./publish-mac-intel.sh
```

## Cross-platform
Rider og .NET kan kryds-kompilere på tværs af OS og CPU-arkitekturer.

---

# 📦 Contributing

Pull requests er velkomne — både forbedringer af HTML-layoutet, flere filtyper, og performance-optimeringer.

---

# 📜 License

MIT License

