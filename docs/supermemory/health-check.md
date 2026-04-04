---
description: Scan and repair the knowledge base
---

# Health Check

The **Health Check** button scans your entire knowledge base for problems and fixes what it can -- like a librarian who keeps the shelves organised.

## What it checks

* **Conflicting terminology** -- the same source term translated differently in different articles
* **Broken links** -- `[[backlinks]]` that point to articles that don't exist
* **Orphaned articles** -- articles that nothing links to (disconnected from the graph)
* **Stale content** -- articles not updated in more than 6 months
* **Duplicate content** -- overlapping articles that should be merged
* **Missing cross-references** -- terms or domains that should be linked but aren't
* **Index accuracy** -- statistics and listings that are out of date

## How it works

The AI produces a detailed report in the chat and automatically applies safe fixes (creating stub articles, updating indexes, fixing broken references). Changes that need human judgement are flagged for review.

{% hint style="warning" %}
**Important:** The AI can and will create, update, and reorganise your vault files when you run Health Check. To stay safe:

* **Keep originals elsewhere.** Don't put your only copy of a glossary or style guide in the vault -- keep the original in its own folder.
* **Back up your vault regularly.** Copy the entire `supermemory` folder to a backup location before running Health Check for the first time, and periodically after that. If something goes wrong, you can simply replace the vault folder with your backup.
* **Review changes in Obsidian.** After running Health Check, open Obsidian and browse the recently modified files to verify the AI made sensible changes. Obsidian's search and graph view make this easy.
{% endhint %}

## See Also

* [Process Inbox](process-inbox.md)
* [Distill](distill.md)
* [SuperMemory](../supermemory.md)
