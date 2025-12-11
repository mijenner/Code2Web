# Code2Web

**Code2Web** er et lille CLI-værktøj, der, fra et aktuelt bibliotek, gennemgår undermapper og genererer en samlet HTML-visning af kode i disse mapper.
Formålet er at lette processen med at: 

- få kode lagt i bilag. Det kan gøres ved at printe fra HTML til pdf. pdf filen kan derefter lægges i forlængelse af synopsis.
- at få **overblik over kode** i en solution. 
- hurtigt at inspicere **de enkelte kode-filer** i en solution. 
- kildekoden vises med **syntax highlighting** så læsning lettes. 

Værktøjet fungerer på både **Windows** og **macOS** (Intel + Apple Silicon).

## Kort fortalt 
### Input folderstruktur: 
```text
C:\Users\mje\source\repos\hold5\
│
├─ solutionA\
│    ├─ Program.cs
│    ├─ Game.cs
│    └─ Utils\
│         └─ MathHelpers.cs
│
├─ solutionB\
│    ├─ main.py
│    └─ helpers.py
│
└─ solutionC\
     ├─ index.html
     └─ script.js
```

### Kørsel 
```powershell
cliCode2Web --class hold5
```

### Resultat 
Resultatet bliver. 
```text 
C:\Users\mje\Documents\Code2Web\hold5\
│
├─ index.html          ← Oversigtsside med links til alle grupper
│
├─ solutionA.html      ← Indeholder al kode fra solutionA
├─ solutionB.html
└─ solutionC.html
```

---
## ✨ Hurtig kom-i-gang - fælles  
- Download binær der passer til dit system fra https://github.com/mijenner/Code2Web/releases/latest (åben i nyt vindue med: ctrl+klik på win, cmd+click på mac)
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
- Gå til denne folder i Finder, og dobbeltklik på index.html for at åbne den i browser. 

---

## ✨ Features

- Programmet Gennemgår en *input-mappe* (hvis du ikke angiver andet, så den mappe du "står i" (command prompt eller terminal))
- Finder relevante kodefiler (C#, Java, Python, XML, JSON, HTML m.fl.)
- Laver én HTML-side pr. underfolder. 
- Laver en samlet *index.html* med links til alle underfolderes HTML filer. 
- Syntax highlighting via Highlight.js
- Fold alle / luk alle-knapper for store filer
- Styrbar folder-udtrækningsdybde (default: **5**)
- Ekskluderer per default ikke "tekniske" foldere (`obj/`, `.idea/`, `.vs/`, `bin/`, `__MACOSX/`)
- Mulighed for at inkludere dem med CLI-flags (`--include-obj`, `--include-idea`, `--include-vs`, `--include-bin`, `--include-json`)

---

# 🚀 Installation

Det er **ikke** nødvendigt at clone repoet for at bruge værktøjet.

Gå til:

**Releases → Latest**  
https://github.com/mijenner/Code2Web/releases/latest

Her finder du zip-filer til:

- cliCode2Web.zip - **Windows**
- cliCode2Web-mac.zip - **macOS ARM (M1/M2/M3)**
- cliCode2Web-mac-intel.zip - **macOS Intel (x64)**

Download den zip-fil, der passer til din maskine, og pak den ud.

---

## 🪟 Windows installation

1. Download `cliCode2Web.zip`
2. Udpak filen
3. Læg `cliCode2Web.exe` i en mappe, fx:

```
C:\Users\<ditbrugernavn>\source\repos\MinSolutionFolder\
```

4. Åben kommandoprompt og gå til folderen:

VS2022 -> Tools -> Commandline -> Developer command prompt. 

```powershell
cd .. 
```

5. Kør værktøjet:

```powershell
cliCode2Web --help
cliCode2Web --class=holdnavn 
```

Og se at det virker ved at åbne den genererede index.html, som per default er i `C:\Users\ditbrugernavn\Documents\Code2Web\holdnavn`

---

## 🍏 macOS installation

1. Download enten:
   - `cliCode2Web-mac.zip` (Apple Silicon)
   - `cliCode2Web-mac-x64.zip` (Intel)

2. Udpak zip-filen

3. Flyt indholdet af zip filen, dvs. filen der hedder noget med cliCode2Web, til en folder, f.eks. til:

```
/Users/<ditbrugernavn>/source/repos/<dinSolutionFolder> 
```

3. Gør filen eksekverbar (bevares normalt af zip, men for en sikkerheds skyld) - fra terminalen:

```bash
cd /Users/<ditbrugernavn>/source/repos/<dinSolutionFolder> 
chmod +x cliCode2Web
```


5. Afprøv programmet - fra terminalen:

```bash
cliCode2Web --help
cliCode2Web
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
