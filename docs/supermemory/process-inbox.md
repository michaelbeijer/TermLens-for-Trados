---
description: Organise raw material into structured knowledge base articles
---

# Process Inbox

The **Process Inbox** button in the Supervertaler Assistant toolbar reads raw material from your `00_INBOX/` folder and uses AI to organise it into structured knowledge base articles -- client profiles, terminology entries, domain knowledge, and style guides.

## How to use

1. Drop raw material into your `supermemory/00_INBOX/` folder: client briefs, glossaries, feedback notes, style guides, reference articles, or anything that helps you translate better
2. Open the **Supervertaler Assistant** panel and look for the SuperMemory toolbar below the context bar
3. The toolbar shows how many files are waiting (e.g. "3 files in inbox")
4. Click **Process Inbox**
5. The AI reads each file, creates structured articles in the appropriate folders, and archives the originals to `00_INBOX/_archive/`

A summary of all created files appears in the chat when processing is complete.

{% hint style="info" %}
The inbox count updates automatically when files are added externally (e.g. via the [Obsidian Web Clipper](obsidian-setup.md#web-clipper)). You can also click the refresh button (↻) on the toolbar to update the count manually.
{% endhint %}

## What gets created

Depending on the content of your raw material, Process Inbox creates articles in one or more of these folders:

| Folder | Article type | Example |
|--------|-------------|---------|
| `01_CLIENTS` | Client profiles | Language preferences, terminology decisions, contact details |
| `02_TERMINOLOGY` | Term articles | Approved translations with rejected alternatives and reasoning |
| `03_DOMAINS` | Domain knowledge | Conventions, common pitfalls, reference material |
| `04_STYLE` | Style guides | Formatting rules, register, localisation conventions |

Each article includes YAML frontmatter with metadata (type, client, domain, languages, date) and backlinks to related articles, building up an interconnected knowledge graph.

## See Also

* [Distill](distill.md) -- extract knowledge from translation files (TMX, DOCX, PDF)
* [Health Check](health-check.md)
* [Obsidian Setup](obsidian-setup.md)
