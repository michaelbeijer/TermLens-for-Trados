# Providers and Models

{% hint style="info" %}
You are viewing help for **Supervertaler for Trados** -- the Trados Studio plugin. Looking for help with the standalone app? Visit [Supervertaler Workbench help](https://help.supervertaler.com).
{% endhint %}

The Supervertaler Assistant supports multiple AI providers. You only need one to get started.

## Switching Models

The current provider and model are shown in the status area at the bottom of the chat panel. You can switch models in two ways:

* **Quick switch** -- click the provider/model label directly. A dropdown menu appears with all available models grouped by provider. The current model is marked with a tick. Select a different model to switch instantly.
* **Settings** -- open the settings dialogue (gear icon) and switch to the **AI Settings** tab for full configuration including API keys, endpoints, and advanced options.

## Supported Providers

| Provider      | Models                                                         |
| ------------- | -------------------------------------------------------------- |
| **OpenAI**    | GPT-5.4, GPT-5.4 Mini                                          |
| **Anthropic** | Claude Sonnet 4.6, Claude Haiku 4.5, Claude Opus 4.6           |
| **Google**    | Gemini 2.5 Flash, Gemini 2.5 Pro, Gemini 3.1 Pro (Preview), Gemma 4 31B, Gemma 4 26B MoE |
| **Grok**      | Grok 4.20, Grok 4.1 Fast, Grok 4.20 (Reasoning)               |
| **Mistral**   | Mistral Large, Mistral Small, Mistral Nemo                     |
| **Ollama**    | TranslateGemma, Qwen 3, Aya Expanse (local, no API key needed) |
| **Custom**    | Any OpenAI-compatible API endpoint                             |

{% hint style="info" %}
If you want privacy or offline use, try [Ollama](https://supervertaler.gitbook.io/supervertaler/ai-translation/ollama) with a local model. No API key or internet connection needed.
{% endhint %}

## Choosing a Model

For everyday translation questions, a smaller and cheaper model like **GPT-5.4 Mini** or **Claude Haiku 4.5** works well. For complex tasks like document analysis, prompt generation, or when you need the highest quality suggestions, use a larger model like **Claude Sonnet 4.6** or **GPT-5.4**.

Some features are provider-specific:

| Feature | Availability |
| ------- | ------------ |
| [Studio Tools](studio-tools.md) | All providers except Ollama |
| Image attachments | All providers with vision support |
| Document attachments | All providers |
| [Memory bank](super-memory/ai-integration.md) context | All providers |

## See Also

* [Supervertaler Assistant](../ai-assistant.md) -- Overview
* [AI Settings](../settings/ai-settings.md) -- API keys, endpoints, advanced options
* [AI Cost Guide](../ai-cost-guide.md) -- Token pricing and cost estimates
