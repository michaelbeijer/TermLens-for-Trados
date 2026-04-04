---
description: How SuperMemory integrates with AI translations and chat
---

# AI Integration

SuperMemory is automatically integrated into all AI-powered features in Supervertaler. When you translate (batch or chat), the AI consults your knowledge base before producing a translation.

## What the AI loads

Before every translation, Supervertaler reads your vault and loads the most relevant articles:

1. **Client profile** -- The AI tries to match your Trados project name against client profiles in `01_CLIENTS/`. If your project is called "Acme Legal Contract 2026", it finds the Acme Corporation profile and loads their language preferences, terminology decisions, and style rules.
2. **Domain knowledge** -- The AI analyses your document to detect the domain (legal, medical, technical, marketing, etc.) and loads the matching article from `03_DOMAINS/` with conventions and common pitfalls.
3. **Style guide** -- The AI loads the most relevant style guide from `04_STYLE/`, preferring client-specific guides over general ones.
4. **Terminology articles** -- The AI loads term articles from `02_TERMINOLOGY/` that match your client, domain, or language pair. These include not just the approved translations, but also rejected alternatives and the reasoning behind each decision.

## How it works with existing context

SuperMemory adds an extra intelligence layer on top of the context you already use:

| Context source | What it provides | How SuperMemory enhances it |
|---|---|---|
| **Termbases** (MultiTerm) | Flat term pairs: term A = term B | Adds the _why_: reasoning, rejected alternatives, client-specific overrides |
| **Translation memories** | Previous translations for style anchoring | Adds domain conventions and style rules |
| **Document content** | Document type detection | Adds specific domain pitfalls and formatting conventions |
| **AutoPrompt** | AI-generated translation instructions | Informed by KB context for more accurate prompt generation |

All of these work together. Termbases give the AI the terms; SuperMemory tells it _why_ those terms were chosen and what to watch out for.

## Memory-aware chat

The Supervertaler Assistant chat window is also memory-aware. When you ask the AI a question about a translation, it has access to your SuperMemory knowledge base alongside the document context, terminology, and TM matches. This means you can ask questions like "What register should I use for this client?" and the AI answers based on your actual KB articles, not generic assumptions.

## Token budget

To avoid overloading the AI's context window, SuperMemory is allocated a token budget (approximately 4000 tokens). If your vault contains more relevant content than fits in the budget, articles are prioritised: client profile first, then domain knowledge, then style guide, then terminology articles.

## Enabling and disabling

SuperMemory context can be toggled on or off in [AI Settings](../settings/ai-settings.md):

* **Include SuperMemory knowledge base in AI context** -- enables KB context for translations and chat
* **Use knowledge base when generating prompts (AutoPrompt)** -- enables KB context when AutoPrompt generates new translation prompts

Both are enabled by default.

## See Also

* [AI Settings](../settings/ai-settings.md)
* [SuperMemory](../supermemory.md)
* [Supervertaler Assistant](../ai-assistant.md)
* [Batch Translate](../batch-translate.md)
