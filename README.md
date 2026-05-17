# Code2Web

**Code2Web** er et lille CLI-værktøj, der, fra et aktuelt bibliotek, gennemgår undermapper og genererer en samlet HTML-visning af kode i disse mapper.
Formålet er at lette processen med at: 

- få kode lagt i bilag. Det kan gøres ved at printe fra HTML til pdf. pdf filen kan derefter lægges i forlængelse af synopsis.
- at få **overblik over kode** i en solution. 
- hurtigt at inspicere **de enkelte kode-filer** i en solution. 
- kildekoden vises med **syntax highlighting** så læsning lettes. 

Værktøjet fungerer på både **Windows** og **macOS** (Intel + Apple Silicon).

---
## ✨ Hurtig kom-i-gang - fælles  
- Download binær der passer til dit system fra https://github.com/mijenner/Code2Web/releases/latest (ctrl+klik på win, cmd+click på mac)
- Pak zip filen ud.
- Kopier indholdet, dvs. den binære fil, aka programmet, til en folder, et niveau over din solution folder (der hvor .sln filen er, en op). 

## ✨ Hurtig kom-i-gang - Windows 
- I VS 2022 vælges -> Tools -> Command line -> Developoer Command Prompt
- Se hvad der er i folderen med `dir`, er der .sln fil er du på rette sted. 
- Skift aktuelt bibliotek til et niveau op med `cd ..`  
- Kør programmet med tilføjelse af holdnavn: `cliCode2Web --class=holdnavn`
- Programmet gennemløber nu underfolderne for kode, og gemmer resultatet, per default i `C:\Users\ditbrugernavn\Documents\Code2Web\holdnavn`
- Gå til denne folder i File Explorer, og dobbeltklik på index.html for at åbne den i browser. 

## ✨ Hurtig kom-i-gang - macOS 
- I Jetbrains Rider vælges højreklik på solution -> Tools -> Open in Terminal
- Se hvad der er i folderen med `ls -al`, er der .sln fil er du på rette sted. 
- Skift aktuelt bibliotek til et niveau op, over .sln filen, med `cd ..` 
- Kør programmet med tilføjelse af holdnavn: `cliCode2Web --class=holdnavn`
- Programmet gennemløber nu underfolderne for kode, og gemmer resultatet, per default i `/Users/ditbrugernavn/Documents/Code2Web/holdnavn`

---

## ✨ Features

- Programmet gennemgår en *input-mappe* (default: den mappe du "står i" — kan overstyres med `--directory`)
- Finder relevante kodefiler og **genkender automatisk projekttype**: C#/.NET (console, Avalonia, MAUI, Blazor/ASP.NET), Unity, Node.js, Next.js og Arduino
- Understøtter bl.a. C#, Razor (`.razor`/`.cshtml`), Java, Python, Arduino, XML/XAML/AXAML, HTML, samt TypeScript/JavaScript-frameworks (React, Vue, Svelte, Astro)
- Laver én HTML-side pr. underfolder
- Laver en samlet *index.html* med links til alle underfolderes HTML filer
- Syntax highlighting via Highlight.js
- Fold alle / luk alle-knapper for store filer
- Styrbar folder-udtrækningsdybde (default: **5**)
- **Magre, gennemtænkte defaults** — C#/.NET og Unity er code-first ud af boksen, så HTML-dokumenterne ikke bliver "busy"
- Per-projekt finjustering via en valgfri `code2web.json` markerfil (se nedenfor)
- Ekskluderer per default ikke "tekniske" foldere (`obj/`, `.idea/`, `.vs/`, `bin/`, `__MACOSX/`)
- Mulighed for at inkludere dem med CLI-flags (`--include-obj`, `--include-idea`, `--include-vs`, `--include-bin`, `--include-json`)

---

# 🚀 Installation

Det er **ikke** nødvendigt at clone repoet for at bruge værktøjet.

Gå til:

**Releases → Latest**  
https://github.com/mijenner/Code2Web/releases/latest

Her finder du zip-filer til:

- **Windows**
- **macOS ARM (M1/M2/M3)**
- **macOS Intel (x64)**

Download den zip-fil, der passer til din maskine, og pak den ud.

---

## 🪟 Windows installation

1. Download `Code2Web-win-x64.zip`
2. Udpak filen
3. Læg `cliCode2Web.exe` i en mappe, fx:

```
C:\Users\<dig>\MinSolutionFolder\
```

4. Kør værktøjet:

```powershell
cliCode2Web --help
```

Og se at det virker. 

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

4. Flyt den til en folder, f.eks.:

```
/Users/<ditbrugernavn>/source/repos/<dinSolutionFolder> 
```

5. Test:

```bash
cliCode2Web --help
```

---

# 🧭 Brug

Gå ind i mappen et niveau over hvor din solution er (se efter .sln fil, og gå så et niveau op):

```
cd "C:\Users\mje\Downloads\hold\2k25"
```

Kør derefter:

På Windows og Apple-mac: 

```bash
cliCode2Web --class 2k25
```

På Intel-mac: 

```bash
cliCode2Web-x64 --class 2k25
```

Dette genererer:

```
~/Documents/Code2Web/2k25/
    index.html
    solutionA.html
    solutionB.html
    solutionC.html
    ...
```

Åbn `index.html` i en browser.

Input er som standard den **mappe du står i**. Vil du pege på en anden mappe uden at skifte aktuelt bibliotek, kan du bruge `--directory <sti>`.

---

# ⚙️ Options

Kør:

```bash
cliCode2Web --help
```

Fuld liste over options:

| Option | Kort | Default | Betydning |
|--------|------|---------|-----------|
| `--class <navn>` | `-c` | *(påkrævet)* | Holdnavn — bruges i standard output-sti |
| `--directory <sti>` | `-d` | nuværende mappe | Rodmappe med elevmapper |
| `--output <folder>` | `-o` | `<Documents>\Code2Web\<class>` | Overstyr default output-folder |
| `--depth <n>` | `-r` | `5` | Rekursionsdybde ved fil-scan i elevmapper |
| `--marker-file <navn>` | | `code2web.json` | Navn på markerfil (se nedenfor) |
| `--marker-depth <n>` | | `2` | Hvor dybt der søges efter markerfil i elevmappen |
| `--profile <type>` | | `auto` | Tving profil for alle grupper: `auto\|unity\|node\|next\|csharp\|arduino\|generic` |
| `--include-obj` | | `false` | Medtag `obj/`-mapper |
| `--include-bin` | | `false` | Medtag `bin/`-mapper |
| `--include-idea` | | `false` | Medtag `.idea/`-mapper (Rider) |
| `--include-vs` | | `false` | Medtag `.vs/`-mapper (Visual Studio) |
| `--include-json` | | `false` | Medtag `.json`-filer som ellers filtreres |
| `--unity-include-yaml` | | `false` | Unity: medtag YAML-assetfiler (`.unity`/`.prefab`/`.asset`/`.mat`/`.controller`/`.anim`) |
| `--unity-include-meta` | | `false` | Unity: medtag `.meta`-filer (GUID mapping) |
| `--unity-include-vendor` | | `false` | Unity: medtag vendor-foldere under Assets (TextMesh Pro, Plugins, Standard Assets) |
| `--quiet` | `-q` | `false` | Kør uden ekstra statuslinjer |

Default-output er:

```
Windows: C:\Users\<dig>\Documents\Code2Web\<class>\
macOS:   /Users/<dig>/Documents/Code2Web/<class>/
```

**Bemærk om JSON:** `.json`-filer filtreres som standard fra, så HTML-dokumenterne ikke bliver "busy". Undtagelsen er Node/Next-profilerne, hvor JSON er inkluderet som default (konfigurationsfiler hører naturligt til der).

**Bemærk om Unity:** Unity-profilen er code-first som default — kun scripts, shaders, asmdef og UI-filer. YAML-assets og vendor-foldere er slået fra som default og kan tændes med de tre `--unity-*`-flag.

---

# 🎯 Per-projekt finjustering med `code2web.json`

De fleste projekter kræver **ingen** opsætning — C#/.NET, Unity, Node og Next genkendes automatisk med magre, fornuftige defaults.

Men i en enkelt elevmappe kan du lægge en fil ved navn `code2web.json` (søges som standard ned til 2 niveauer) for at styre netop dét projekt. Det er nyttigt til usædvanlige projekter, hvor du vil have lidt ekstra med — eller skære lidt fra.

Felter i `code2web.json`:

| Felt   | Betydning |
|--------|-----------|
| `schema` | Skemaversion (sæt `1`) |
| `type` | Tving projekttype: `auto`, `unity`, `node`, `next`, `csharp`, `arduino`, `generic` |
| `name` | Vist navn for projektet i HTML-outputtet |
| `tune` | Finjusterings-objekt — gælder **alle** typer |

`tune` kan indeholde:

| Felt               | Betydning |
|--------------------|-----------|
| `addExtensions`    | Ekstra filendelser ud over profilens standard (fx `".json"`, `".sql"`) |
| `removeExtensions` | Fjern filendelser fra profilens standard (trim støj) |
| `excludeFolders`   | Ekstra mapper der udelades |
| `includeFolders`   | Vis kun disse undermapper (relativt til projektroden) |

Markerfilen selv vises aldrig i outputtet.

### Eksempler

**Navngiv et projekt pænt:**
```json
{ "schema": 1, "type": "csharp", "name": "Eksamensprojekt – Gruppe 4" }
```

**Usædvanligt C#-projekt der skal have ekstra filer med:**
```json
{
  "schema": 1,
  "type": "csharp",
  "tune": {
    "addExtensions": [".json", ".sql"],
    "excludeFolders": ["TestData", "Migrations"]
  }
}
```

**Trim et "busy" web-projekt ned til det væsentlige:**
```json
{
  "schema": 1,
  "type": "node",
  "tune": {
    "removeExtensions": [".css", ".md"],
    "includeFolders": ["src"]
  }
}
```

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
powershell -ExecutionPolicy Bypass -File .\publish-win.ps1
```

## Publish (macOS ARM)
```
./publish-mac.sh
```

## Publish (macOS Intel)
```
./publish-mac-intel.sh
```


---

# 📜 License

MIT License
