# Studio Tools

{% hint style="info" %}
You are viewing help for **Supervertaler for Trados** – the Trados Studio plugin. Looking for help with the standalone app? Visit [Supervertaler Workbench help](https://help.supervertaler.com).
{% endhint %}

Studio Tools lets you query your Trados Studio installation using natural language in the Supervertaler Assistant chat. Instead of navigating through menus and dialogs, you can simply ask the assistant about your projects, translation memories, and project templates — and it will look up the answer for you.

{% hint style="success" %}
Studio Tools requires **Claude** as your AI provider. Other providers (OpenAI, Gemini, etc.) do not support this feature and will work as before — plain chat without Trados queries.
{% endhint %}

## How It Works

When you send a message in the Supervertaler Assistant, Claude automatically decides whether it needs to query Trados Studio to answer your question. If it does, it calls the appropriate tool behind the scenes, reads the result, and presents the information in a clear format.

You do not need to use any special syntax or commands. Just ask your question naturally.

While a tool is running, the thinking indicator shows what is happening — for example, "Checking Trados projects…" or "Listing translation memories…".

## Available Tools

| Tool | What It Does |
| ---- | ------------ |
| **List Projects** | Lists all projects registered in Trados Studio with their name, status, and creation date |
| **Get Project Details** | Shows detailed information about a specific project, including source and target languages, files, and folder path |
| **List Translation Memories** | Lists all TMs found in your Studio TM folder |
| **List Project Templates** | Lists all project templates available in Trados Studio |

## Example Questions

Here are some things you can try asking in the Assistant chat:

### Projects

* "What projects do I have in Trados Studio?"
* "Show me all my in-progress projects"
* "How many projects do I have?"
* "Tell me about the Client Alpha project"
* "What languages does the Client Alpha project use?"
* "What files are in my latest project?"
* "Do I have any completed projects?"
* "Which project was created most recently?"

### Translation Memories

* "What translation memories do I have?"
* "List my TMs"
* "Do I have a TM for English to Dutch?"

### Project Templates

* "What project templates are available?"
* "List my templates"

### Combined Questions

You can combine Studio Tools queries with the assistant's regular translation capabilities:

* "What projects am I working on? And can you also translate this segment?"
* "List my projects, then explain the terminology in the current segment"

The assistant handles the tool call first, then continues with the rest of your question seamlessly.

## Technical Details

Studio Tools reads data directly from your local Trados Studio installation. Specifically:

* **Projects** are read from the `projects.xml` file in your Documents folder (e.g., `Documents\Studio 2024\Projects\projects.xml`). Project details are read from the individual `.sdlproj` files.
* **Translation memories** are found by scanning the `Translation Memories` folder and any TMs referenced in your projects.
* **Project templates** are found in the `Project Templates` folder.

No data is sent to external services other than the AI provider. The tool results are passed to Claude as part of the conversation so it can format and present them to you.

{% hint style="info" %}
Studio Tools currently provides **read-only** access to your Trados data. It cannot create, modify, or delete projects, TMs, or templates.
{% endhint %}

## See Also

* [Supervertaler Assistant](ai-assistant.md) — The chat interface where Studio Tools is available
* [AI Settings](settings/ai-settings.md) — Configure your AI provider (must be set to Claude for tool use)
