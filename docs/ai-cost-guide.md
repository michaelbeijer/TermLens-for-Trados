{% hint style="info" %}
You are viewing help for **Supervertaler for Trados** — the Trados Studio plugin. Looking for help with the standalone app? Visit [Supervertaler Workbench help](https://help.supervertaler.com).
{% endhint %}

This page helps you estimate the API cost of using AI features in Supervertaler for Trados. All prices are based on official provider pricing as of March 2026 and are shown in **US dollars**.

{% hint style="info" %}
AI provider costs are **separate** from your Supervertaler licence. You pay the AI provider directly for the tokens your requests consume. Supervertaler does not add any markup.
{% endhint %}

## How costs are calculated

AI providers charge per **token** — a unit of text roughly equal to ¾ of a word. Costs depend on:

* **Input tokens** — the text you send (source segment, system prompt, terminology context)
* **Output tokens** — the text the model returns (translated segment, proofread text, generated prompt)

Because Supervertaler translates **segment by segment**, the system prompt and terminology context are included with every segment. For a typical 5,000-word document (~250 segments), this means:

| Task | Input tokens | Output tokens |
|------|-------------|---------------|
| **Batch Translate** | ~125,000 | ~8,000 |
| **AI Proofreader** | ~140,000 | ~8,000 |
| **AutoPrompt** | ~10,000 | ~2,000 |

These are estimates for a representative document. Actual usage varies with segment length, terminology context size, and prompt complexity.

## Cost per 5,000-word document

### OpenAI

| Model | Translate | Proofread | AutoPrompt |
|-------|-----------|-----------|-----------------|
| **GPT-4.1** (recommended) | $0.31 | $0.34 | $0.04 |
| **GPT-4.1 Mini** | $0.06 | $0.07 | $0.01 |
| **GPT-5.4** | $0.33 | $0.36 | $0.05 |
| **o4-mini** (reasoning) | — | — | $0.02 |

### Claude (Anthropic)

| Model | Translate | Proofread | AutoPrompt |
|-------|-----------|-----------|-----------------|
| **Claude Sonnet 4.6** (recommended) | $0.50 | $0.54 | $0.06 |
| **Claude Haiku 4.5** | $0.17 | $0.18 | $0.02 |
| **Claude Opus 4.6** | $0.83 | $0.90 | $0.10 |

### Google Gemini

| Model | Translate | Proofread | AutoPrompt |
|-------|-----------|-----------|-----------------|
| **Gemini 2.5 Flash** (recommended) | $0.06 | $0.06 | $0.01 |
| **Gemini 2.5 Pro** | $0.24 | $0.26 | $0.03 |
| **Gemini 3.1 Pro** (preview) | $0.35 | $0.38 | $0.04 |

### Grok (xAI)

| Model | Translate | Proofread | AutoPrompt |
|-------|-----------|-----------|-----------------|
| **Grok 4.20** (recommended) | $0.30 | $0.33 | $0.03 |
| **Grok 4.1 Fast** | $0.03 | $0.03 | < $0.01 |
| **Grok 4.20 Reasoning** | — | — | $0.09 |

### Ollama (local)

| Model | Translate | Proofread | AutoPrompt |
|-------|-----------|-----------|-----------------|
| **TranslateGemma 12B** | Free | Free | Free |
| **TranslateGemma 4B** | Free | Free | Free |
| **Qwen 3 14B** | Free | Free | Free |
| **Aya Expanse 8B** | Free | Free | Free |

{% hint style="success" %}
**Ollama models run on your own computer** — there are no API costs. The trade-off is that quality depends on your hardware and the models are generally less capable than cloud-hosted models. See [AI Settings](settings/ai-settings.md) for setup instructions.
{% endhint %}

## Our recommendation

{% hint style="success" %}
**If you could only pick one model for everything — translation, proofreading, and chat — we would recommend Claude Sonnet 4.6.** It follows translation instructions precisely, handles terminology constraints well, is fast enough for batch operations, and delivers consistently high quality across legal, technical, and general content. It costs roughly $0.50 per 5,000-word document, which is a fraction of a cent per segment.
{% endhint %}

For budget-conscious batch work, **GPT-4.1 Mini** or **Gemini 2.5 Flash** offer excellent quality at a fifth of the price. For the absolute highest quality on specialised content, **Claude Opus 4.6** or **GPT-5.4** are worth the premium.

## Token pricing reference

For reference, these are the per-token rates used in the calculations above:

| Model | Input (per 1M tokens) | Output (per 1M tokens) |
|-------|----------------------|------------------------|
| GPT-4.1 | $2.00 | $8.00 |
| GPT-4.1 Mini | $0.40 | $1.60 |
| GPT-5.4 | $1.75 | $14.00 |
| o4-mini | $1.10 | $4.40 |
| Claude Sonnet 4.6 | $3.00 | $15.00 |
| Claude Haiku 4.5 | $1.00 | $5.00 |
| Claude Opus 4.6 | $5.00 | $25.00 |
| Gemini 2.5 Flash | $0.30 | $2.50 |
| Gemini 2.5 Pro | $1.25 | $10.00 |
| Gemini 3.1 Pro (Preview) | $2.00 | $12.00 |
| Grok 4.20 | $2.00 | $6.00 |
| Grok 4.1 Fast | $0.20 | $0.50 |
| Grok 4.20 (Reasoning) | $2.00 | $6.00 |

{% hint style="warning" %}
Prices change regularly. Check your provider's pricing page for the latest rates:
[OpenAI](https://openai.com/api/pricing/) · [Anthropic](https://www.anthropic.com/pricing#anthropic-api) · [Google Gemini](https://ai.google.dev/gemini-api/docs/pricing) · [xAI](https://docs.x.ai/developers/models)
{% endhint %}

## Tips for managing costs

* **Start with a budget model** — GPT-4.1 Mini, Gemini 2.5 Flash, or Grok 4.1 Fast are excellent for routine translation at a fraction of the cost.
* **Use premium models selectively** — reserve GPT-5.4, Claude Opus, or Gemini 2.5 Pro for specialised content (legal, medical, patents) where the quality difference justifies the cost.
* **Reasoning models are for AutoPrompt only** — o4-mini and Grok 4.20 Reasoning think step-by-step, which is valuable for AutoPrompt (analysing a project and generating a prompt), but adds unnecessary cost and latency for translation.
* **Try Ollama for zero cost** — if you have a computer with 8+ GB of RAM, TranslateGemma 12B delivers surprisingly good results for free.
* **Check your usage** — the [Usage Statistics](settings/usage-statistics.md) tab in Settings tracks your token consumption per provider.

## See also

* [AI Settings](settings/ai-settings.md) — configure your API keys and choose a model
* [Batch Translate](batch-translate.md) — translate segments in bulk
* [AI Proofreader](ai-proofreader.md) — proofread translated segments
* [AutoPrompt](generate-prompt.md) — generate translation prompts
* [Licensing & Pricing](licensing.md) — Supervertaler subscription plans
