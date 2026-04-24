{% hint style="info" %}
You are viewing help for **Supervertaler for Trados** – the Trados Studio plugin. Looking for help with the standalone app? Visit [Supervertaler Workbench help](https://help.supervertaler.com).
{% endhint %}

Supervertaler for Trados uses the same SQLite termbase format as Supervertaler Workbench. You manage your termbases through the Settings dialogue.

## Accessing termbase settings

1. Click the **gear icon** in the TermLens panel, or go to **Settings** in the plugin ribbon
2. Switch to the **TermLens** tab

## Database file

The plugin stores all termbases in a single `.db` file (SQLite database).

- Click **Browse** to select an existing database file
- Click **Create New** to create a fresh, empty database

{% hint style="info" %}
The `.db` file uses the same Supervertaler SQLite format as the standalone application. On Windows, you can share the same termbase file between both tools by pointing them to the same data folder. On a Mac with Parallels, see the note below.
{% endhint %}

## MultiTerm termbases

If your Trados project has MultiTerm termbases (`.sdltb` files) attached, they appear automatically at the bottom of the termbase list with a **[MultiTerm]** label and green background. These termbases are read-only in TermLens –to manage their terms, use Trados's built-in MultiTerm interface. See [MultiTerm Support](multiterm-support.md) for full details.

## Termbase list

Once a database is loaded, the termbase list shows all Supervertaler termbases it contains, plus any detected MultiTerm termbases. Each Supervertaler termbase has three toggles:

| Toggle | Purpose |
|--------|---------|
| **Read** | Load terms from this termbase for matching in TermLens |
| **Write** | New terms added via [quick-add shortcuts](termlens/adding-terms.md) go into this termbase |
| **Project** | Designate as the project termbase (terms shown in pink, prioritised in matching) |

{% hint style="warning" %}
Only one termbase can be marked as **Project** at a time. Setting a new project termbase clears the flag from the previous one.
{% endhint %}

## Creating a new termbase

1. Click **Add Termbase**
2. Enter a **name** for the termbase
3. Select the **source language** and **target language**
4. Click **OK**

The new termbase appears in the list, ready for use.

## Import from TSV

You can import terminology from a tab-separated values file:

1. Select the target termbase in the list
2. Click **Import from TSV**
3. Select your `.tsv` file
4. A confirmation dialog shows the filename, row count, termbase name, and language pair -- check that you are importing into the right termbase
5. A progress bar tracks the import (useful for large glossaries with thousands of terms)

**File format:**

The first row must be a header row. Recognised column headers (case-insensitive):

| Column | Required | Recognised headers |
|--------|----------|-------------------|
| **Source** | Yes | `Source`, `Source Term`, `Src`, or a language name (e.g., `English`) |
| **Target** | Yes | `Target`, `Target Term`, `Tgt`, or a language name (e.g., `Dutch`) |
| Term UUID | No | `Term UUID`, `UUID`, `Term ID`, `ID` |
| Priority | No | `Priority`, `Prio`, `Rank` |
| Domain | No | `Domain`, `Subject`, `Field`, `Category` |
| Notes | No | `Notes`, `Note`, `Definition`, `Comment` |
| Project | No | `Project` |
| Client | No | `Client`, `Customer` |
| Forbidden | No | `Forbidden`, `Do not use` |

For terms with multiple synonyms, use pipe-delimited values: `main|synonym1|synonym2`. Forbidden synonyms are wrapped as `[!term]`.

**Example:**

```
Source	Target	Domain	Notes
database	databank|gegevensbank		
software	software		Non-translatable
user interface	gebruikersinterface|gebruikersomgeving	IT	
```

{% hint style="info" %}
TSV files exported from Supervertaler (both the Trados plugin and Workbench) can always be reimported without any changes. Files from other tools are also supported as long as they have recognisable column headers.
{% endhint %}

## Export to TSV

To export all terms from a termbase:

1. Select the termbase in the list
2. Click **Export to TSV**
3. Choose a save location

The exported file uses tab-separated columns with a header row: `Term UUID`, `Source`, `Target`, `Priority`, `Domain`, `Notes`, `Project`, `Client`, `Forbidden`. Synonyms are pipe-delimited and forbidden synonyms are marked with `[!term]`. The file is UTF-8 encoded with BOM for Excel compatibility.

## Termbase Editor

For full editing capabilities, double-click a termbase in the list to open the **Termbase Editor**. From here you can:

- **Search** for terms by source or target text
- **Edit** individual term entries
- **Delete** terms
- Perform **bulk operations** (e.g. bulk delete, bulk reverse)

### Right-click menu

Right-clicking any row in the grid opens a context menu with the following actions:

- **Copy cell** – copies the content of the clicked cell to the clipboard.
- **Edit term…** – opens the full term entry editor for the clicked row.
- **Reverse source/target** – swaps the source and target for the selected rows (see below).
- **Delete term** – deletes the selected rows after confirmation.

Multi-row selection is preserved: if you select several rows first and then right-click on one of them, the selection stays intact so actions apply to all selected entries. If you right-click on a row that wasn't already selected, the selection collapses to just that row.

### Reversing source/target

If you have term entries that ended up in the wrong direction – for example, English text in the Dutch column when the termbase is declared English → Dutch – you can correct them with **Reverse source/target**:

1. Select one or more rows in the grid (Shift-click or Ctrl-click for multi-select).
2. Right-click → **Reverse source/target (N entries)**.
3. Confirm.

The operation swaps the source and target text, language tags, abbreviations, and flips the direction of every linked synonym. It runs in a single database transaction, so a partial failure leaves the termbase untouched.

This action is mostly for repairing legacy entries created or edited under v4.19.24 or earlier, when the term entry editor could write values into the wrong DB columns in projects whose direction was the inverse of the termbase's. From v4.19.25 onwards the editor guards against that, so new entries should not need this repair.

{% hint style="info" %}
**Add and Edit dialog fields are always in termbase direction.** The dialog labels and values both reflect the termbase's declared direction – English on the left when the termbase is declared EN→NL, regardless of the current Trados project's direction. From v4.19.25 the values are guaranteed to align with the labels: the Edit dialog re-reads the entry from the database, and the Add dialog swaps the pre-fills internally when the project direction is the inverse of the termbase. Earlier versions could silently write reversed entries in inverse-direction projects – use **Reverse source/target** above to repair any pre-v4.19.25 damage.
{% endhint %}

## Sharing termbases

{% hint style="success" %}
**Tip:** Keep the `.db` file on a network drive or cloud-synced folder (OneDrive, Dropbox, Google Drive) to share termbases across machines and with colleagues. Since both the Trados plugin and Supervertaler Workbench use the same format, everyone can work from the same terminology.
{% endhint %}

{% hint style="warning" %}
**Mac users (Parallels):** On a Mac, Supervertaler Workbench runs natively on macOS while the Trados plugin runs inside Parallels (Windows). The two products cannot share the same `.db` file directly because the Trados plugin must store its data on the Windows side (`C:\Users\...`) – not on the Mac-side shared folder (`\\Mac\Home\...`). To keep your termbases in sync, export from one side and import on the other after making changes. This is a limitation of Parallels' virtual network filesystem, not of the termbase format itself.
{% endhint %}

## Distill into a memory bank

You can extract knowledge from any termbase and add it to a [memory bank](ai-assistant/super-memory.md) using the **Distill** feature:

1. Right-click a termbase in the list
2. Select **⚗ Distill into memory bank**

The AI analyses all terms in the termbase and creates structured articles (terminology decisions, domain knowledge) in the active memory bank's inbox. See [Distill](ai-assistant/super-memory/distill.md) for full details.

---

## See Also

- [MultiTerm Support](multiterm-support.md)
- [TermLens Settings](settings/termlens.md)
- [Adding & Editing Terms](termlens/adding-terms.md)
- [Distill](ai-assistant/super-memory/distill.md)
- [Glossary Basics (Workbench)](https://supervertaler.gitbook.io/supervertaler/glossaries/basics)
