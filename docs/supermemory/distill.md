---
description: Extract knowledge from translation files using AI
---

# Distill

**Distill** extracts knowledge from professional translation files and creates structured knowledge base articles in your SuperMemory inbox. Instead of manually reading through a 50,000-segment translation memory or a 30-page client style guide, the AI analyses the content and distils it into actionable articles: terminology decisions, style conventions, client preferences, and domain knowledge.

## Supported formats

| Format | Extension | What the AI extracts |
|--------|-----------|---------------------|
| **Translation Memory** | `.tmx` | Terminology patterns, consistent style choices, domain-specific phrasing |
| **MultiTerm termbase** | `.sdltb`, `.xml` | Term pairs with definitions, domains, usage notes |
| **Word document** | `.docx` | Style rules, client preferences, formatting conventions, terminology |
| **PDF** | `.pdf` | Style guides, reference material, glossaries, specifications |
| **Excel / CSV** | `.xlsx`, `.csv`, `.tsv` | Glossaries, terminology lists, term pairs |
| **TBX termbase** | `.tbx` | Term entries with metadata |
| **Plain text** | `.txt` | Notes, guidelines, reference material |

## How to use

1. Click the **Distill** button (⚗) on the SuperMemory toolbar in the Supervertaler Assistant panel
2. Select one or more files in the file picker dialog
3. The AI analyses the content and creates draft articles in your `00_INBOX` folder
4. Review the draft articles in Obsidian, then click **[Process Inbox](process-inbox.md)** to compile them into your knowledge base

## What the AI produces

Depending on the source material, Distill creates one or more Markdown articles containing:

* **Terminology decisions** -- terms the translator consistently chose, with reasoning inferred from context and usage patterns
* **Style profile** -- register, voice, formatting conventions, and writing patterns observed across the translations
* **Client preferences** -- conventions specific to the client or project (e.g. "always use 'Schedule' instead of 'Appendix' in procurement documents")
* **Domain knowledge** -- subject-matter conventions, technical vocabulary, and common pitfalls identified from the source material

### Example: Distilling a TMX

A translation memory with 10,000 Dutch-English legal segments might produce:

* A **terminology article** listing the key legal terms with the translations used and why (e.g. "overeenkomst → agreement (not contract), because the client uses 'contract' only for formal notarial documents")
* A **style article** noting the register (formal, third-person, passive voice) and formatting conventions (numbered clauses, capitalised defined terms)
* A **domain article** capturing Dutch legal system conventions relevant to translation (e.g. "Dutch notarial acts use specific formulaic language that should be preserved, not naturalised")

### Example: Distilling a client style guide

A 20-page Word document from a client might produce:

* A **client profile** with their language preferences, terminology decisions, and contact details
* A **style article** with their formatting rules, preferred register, and localisation conventions
* A **terminology article** with their approved terms and rejected alternatives

## Tips

* **Start with your most important client.** Distill their largest TM first -- you'll immediately see the value as the AI surfaces terminology patterns you may not have been consciously aware of.
* **Combine sources.** Select a client's TM, their style guide PDF, and their glossary Excel file together -- the AI cross-references them to produce richer articles.
* **Review before processing.** Distill outputs draft articles to the inbox, not directly to the knowledge base. Always review them in Obsidian before running Process Inbox.
* **Large files are truncated.** Very large TMX files (100K+ segments) are automatically truncated to fit the AI's context window. For best results with huge TMs, export a representative subset first.

## See Also

* [Process Inbox](process-inbox.md)
* [Quick Add](quick-add.md)
* [AI Settings](../settings/ai-settings.md)
