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
- Pak zip filen ud. Du får både binæren og en `references/`-mappe med skabelon-referencer som værktøjet bruger til at klassificere elev-kode.
- Kopier indholdet (binæren *og* `references/`-mappen) til en folder, et niveau over din solution folder.

## ✨ Hurtig kom-i-gang - Windows
- I VS 2022 vælges -> Tools -> Command line -> Developer Command Prompt
- Se hvad der er i folderen med `dir`, er der .sln fil er du på rette sted.
- Skift aktuelt bibliotek til et niveau op med `cd ..`
- Kør programmet med tilføjelse af holdnavn: `cliCode2Web --class=holdnavn`
- Programmet gennemløber nu underfolderne for kode, og gemmer resultatet, per default i `C:\Users\ditbrugernavn\Documents\Code2Web\holdnavn`
- Gå til denne folder i File Explorer, og dobbeltklik på index.html for at åbne den i browser.

## ✨ Hurtig kom-i-gang - macOS
- I JetBrains Rider vælges højreklik på solution -> Tools -> Open in Terminal
- Se hvad der er i folderen med `ls -al`, er der .sln fil er du på rette sted.
- Skift aktuelt bibliotek til et niveau op, over .sln filen, med `cd ..`
- Kør programmet med tilføjelse af holdnavn: `cliCode2Web --class=holdnavn`
- Programmet gennemløber nu underfolderne for kode, og gemmer resultatet, per default i `/Users/ditbrugernavn/Documents/Code2Web/holdnavn`

---

## ✨ Features

- Programmet gennemgår en *input-mappe* (default: den mappe du "står i" — kan overstyres med `--directory`)
- Finder relevante kodefiler og **genkender automatisk projekttype**: C#/.NET (console, Avalonia, MAUI, Blazor/ASP.NET), Unity, Node.js, Next.js og Arduino
- Understøtter bl.a. C#, Razor (`.razor`/`.cshtml`), Java, Python, Arduino, XML/XAML/AXAML, HTML, samt TypeScript/JavaScript-frameworks (React, Vue, Svelte, Astro)
- **Klassificerer hver fil som Bidrag, Skabelon-Interessant eller Skabelon** baseret på sti-matchning mod et bibliotek af reference-skabeloner
- **Ressource-sektion** med alfabetisk liste af billed-/lyd-/asset-filer (Unity-projekter skanner `Assets/` med en kurateret extension-liste)
- Laver én HTML-side pr. elevgruppe med kollapserbare sektioner
- Laver en samlet *index.html* med links til alle gruppers HTML-filer
- Syntax highlighting via Highlight.js
- Knapper til "Fold alle ud", "Luk alle" og "Vis/skjul skabelonkode"
- **Magre, gennemtænkte defaults** — C#/.NET og Unity er code-first ud af boksen, så HTML-dokumenterne ikke bliver "busy"
- Per-projekt finjustering via en valgfri `code2web.json` markerfil
- Ekskluderer per default "tekniske" foldere (`obj/`, `bin/`, `.idea/`, `.vs/`, `.vscode/`, `node_modules/`, `__MACOSX/`)
- Mulighed for at inkludere dem med CLI-flags (`--include-obj`, `--include-bin`, `--include-idea`, `--include-vs`, `--include-json`)

---

# 🚀 Installation

Det er **ikke** nødvendigt at clone repoet for at bruge værktøjet.

Gå til:

**Releases → Latest**
https://github.com/mijenner/Code2Web/releases/latest

Her finder du zip-filer til:

- **Windows**
- **macOS ARM (M1/M2/M3+)**
- **macOS Intel (x64)**

Download den zip-fil, der passer til din maskine, og pak den ud.

**Vigtigt:** zip'en indeholder både binæren *og* en `references/`-mappe. Behold dem sammen — værktøjet leder efter `references/` ved siden af binæren. Hvis du mister `references/`, fungerer alt stadig, men klassifikationen vil markere alt som "Bidrag" (det sikre default).

---

## 🪟 Windows installation

1. Download `Code2Web-win-x64.zip`
2. Udpak filen
3. Læg `cliCode2Web.exe` og den medfølgende `references\`-mappe i en mappe, fx:

```
C:\Users\<dig>\MinSolutionFolder\
```

4. Kør værktøjet:

```
cliCode2Web --help
```

Og se at det virker.

---

## 🍏 macOS installation

1. Download enten:
   - `Code2Web-macos-arm64.zip` (Apple Silicon)
   - `Code2Web-macos-x64.zip` (Intel)

2. Udpak zip-filen
3. Gør binæren eksekverbar (bevares normalt af zip, men for en sikkerheds skyld):

```bash
chmod +x cliCode2Web
```

4. Flyt binæren og den medfølgende `references/`-mappe til en folder, fx:

```
/Users/<ditbrugernavn>/source/repos/<dinSolutionFolder>
```

5. Test:

```bash
cliCode2Web --help
```

---

# 🧭 Brug — det normale flow

For de fleste elev-/eksamens-gennemgange er der kun ét trin:

```bash
cd "C:\Users\mje\Downloads\hold\h2k25"
cliCode2Web --class=h2k25
```

Det opretter HTML i `C:\Users\mje\Documents\Code2Web\h2k25\` — åbn `index.html` i en browser.

På Intel-mac hedder binæren `cliCode2Web-x64`:

```bash
cliCode2Web-x64 --class=h2k25
```

## Det udvidede flow med klassifikation (Bidrag / Skabelon)

Hvis du gerne vil have HTML'en opdelt i **Bidrag**, **Skabelon-Interessant** og **Skabelon**-sektioner, så en censor hurtigt kan finde elevens egne tilføjelser, er der ét ekstra trin:

```bash
cd "C:\Users\mje\Downloads\hold\h2k25"
cliCode2Web --prerun            # skriver code2web-plan.txt i hver produktmappe
                                # (åbn dem og ret evt. tags i hånden)
cliCode2Web --class=h2k25       # genererer HTML med sektioner
```

`--prerun` matcher elevprojektets filer mod skabelon-referencerne (dem i `references/`-mappen ved siden af binæren) og klassificerer hver fil. Resultatet skrives som en `code2web-plan.txt` i hver elevs produktmappe. Du kan åbne den og finjustere — ret fx `t;Eksamensprojekt/Program.cs` til `s;Eksamensprojekt/Program.cs` hvis eleven har skrevet den om.

Tag-betydningen i planen:
- `s` = **bidrag** (elevens egen kode)
- `i` = **skabelon-interessant** (skabelon-fil, men typisk modificeret)
- `t` = **skabelon** (uændret boilerplate)

Når du derefter kører `cliCode2Web --class=h2k25`, læses planen og HTML'en deles op i de tre sektioner.

`--prerun` alene fletter additivt — eksisterende plan-filer beholder dine håndsatte tags, og kun nyopdagede filer føjes til. Brug `--prerun --overwrite` hvis du vil genskabe planen fra bunden.

---

# ⚙️ Options

Kør:

```bash
cliCode2Web --help
```

Fuld liste over options:

| Option | Kort | Default | Betydning |
|--------|------|---------|-----------|
| `--class <navn>` | `-c` | *(påkrævet i normal kørsel)* | Holdnavn — bruges i standard output-sti |
| `--directory <sti>` | `-d` | nuværende mappe | Rodmappe med elevmapper |
| `--output <folder>` | `-o` | `<Documents>\Code2Web\<class>` | Overstyr default output-folder |
| `--depth <n>` | `-r` | `5` | Rekursionsdybde ved fil-scan i elevmapper |
| `--prerun` | | | Skriv `code2web-plan.txt` pr. produkt (klassifikations-forberedelse) |
| `--overwrite` | | | Sammen med `--prerun`: genskab planer fra bunden |
| `--make-reference` | | | Reference-tilstand: udtræk skabelon-referencer fra projekter under `--directory` (avanceret, til vedligehold af reference-biblioteket) |
| `--marker-file <navn>` | | `code2web.json` | Navn på markerfil |
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

**Bemærk om JSON:** `.json`-filer filtreres som standard fra, så HTML-dokumenterne ikke bliver "busy". Undtagelsen er Node/Next-profilerne, hvor JSON er inkluderet som default.

**Bemærk om Unity:** Unity-profilen er code-first som default — kun scripts, shaders, asmdef og UI-filer. YAML-assets og vendor-foldere er slået fra som default og kan tændes med `--unity-*`-flagene.

---

# 🎯 Per-projekt finjustering med `code2web.json`

De fleste projekter kræver **ingen** opsætning — værktøjet genkender og klassificerer automatisk. Men i en enkelt elevmappe kan du lægge en fil ved navn `code2web.json` (søges som standard ned til 2 niveauer) for at styre netop dét projekt — eller markere et delprojekt som hørende til en bestemt skabelon-reference ved navns nævnelse.

Felter:

| Felt   | Betydning |
|--------|-----------|
| `schema` | Skemaversion (sæt `1`) |
| `type` | Tving projekttype: `auto`, `unity`, `node`, `next`, `csharp`, `arduino`, `generic` |
| `name` | Vist navn for projektet i HTML-outputtet, **og** navn på reference til klassifikation |
| `tune` | Finjusterings-objekt — gælder **alle** typer |

`tune` kan indeholde:

| Felt               | Betydning |
|--------------------|-----------|
| `addExtensions`    | Ekstra filendelser ud over profilens standard (fx `".json"`, `".sql"`) |
| `removeExtensions` | Fjern filendelser fra profilens standard (trim støj) |
| `excludeFolders`   | Ekstra mapper der udelades |
| `includeFolders`   | Vis kun disse undermapper (relativt til projektroden) |

Markerfilen vises aldrig i outputtet.

### Eksempler

**Navngiv et projekt og pege på en bestemt reference:**
```json
{ "schema": 1, "type": "csharp", "name": "AvaloniaMVVMApp" }
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

# 🔍 Interesseret i kildekoden?

Hele værktøjet er open source og skrevet i **C# / .NET 8**.

## Krav
- Visual Studio 2022 (Windows) eller JetBrains Rider (macOS eller Windows)
- .NET SDK 8

## Build
```
dotnet build
```

## Publish (Windows)
```
publish-win.cmd
```

## Publish (macOS ARM)
```
./publish-mac.sh
```

## Publish (macOS Intel)
```
./publish-mac-intel.sh
```

Hvert publish-script kopierer `references-shipped/` fra repoet ind ved siden af binæren. Hvis du har forfinet dine egne referencer i `Documents\Code2Web\references\`, så kør `sync-references.cmd` (Windows) eller `sync-references.sh` (mac/linux) først — det synkroniserer dine lokale referencer ind i repoets `references-shipped/`, så de bliver versionerede og kommer med næste publish.

## Automatiske releases via GitHub Actions

Repoet har en workflow i `.github/workflows/release.yml` der bygger for alle tre platforme automatisk når du pusher en `v*`-tag:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Workflowet bygger Windows, macOS ARM og macOS Intel parallelt på GitHubs servere og uploader zip-filerne til en GitHub Release — dine eventuelle `references-shipped/` følger med ind i hver zip.

---

# 📜 License

MIT License
