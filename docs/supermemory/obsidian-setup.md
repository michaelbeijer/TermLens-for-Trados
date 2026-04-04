---
description: Setting up Obsidian for SuperMemory
---

# Obsidian Setup

SuperMemory stores all knowledge as Markdown files, which you can browse and edit with any text editor. For the best experience, we recommend [Obsidian](https://obsidian.md/) -- a free knowledge-base app that visualises the links between your articles as an interactive graph.

## Installing Obsidian

1. Download Obsidian from [https://obsidian.md/download](https://obsidian.md/download) (available for Windows, Mac, and Linux)
2.  Install and open it -- choose **Open folder as vault** and select your SuperMemory folder:

    ```
    C:\Users\{you}\Supervertaler\supermemory\
    ```
3. The free version of Obsidian includes everything you need -- no subscription required. (The paid Sync and Publish add-ons are not needed for SuperMemory.)

## Web Clipper

The [Obsidian Web Clipper](https://obsidian.md/clipper) is a free browser extension that lets you clip web pages directly into your SuperMemory inbox. Install it for Chrome, Firefox, Safari, or Edge.

### Setting up the Web Clipper

1. Install the extension from [obsidian.md/clipper](https://obsidian.md/clipper)
2. Make sure Obsidian is running with your SuperMemory vault open
3. Click the Web Clipper icon in your browser toolbar, then the gear icon (settings)
4. Create a new template (e.g. "supermemory") and set:
   * **Note location:** `00_INBOX`
   * **Vault:** select your supermemory vault
5. Optionally add properties: `source_url` = `{{url}}`, `clipped` = `{{date}}`

Now when you find a useful reference -- a client style guide, a terminology resource, a domain article -- click the clipper, hit save, and it drops straight into your inbox. Next time you click **[Process Inbox](process-inbox.md)**, the AI organises it into structured articles.

## Recommended plugins

These free Obsidian community plugins enhance the SuperMemory experience:

* **Dataview** -- query your vault like a database (e.g. list all terminology articles for a specific client)
* **Calendar** -- visualise when articles were created or modified
* **Graph Analysis** -- enhanced graph view with clustering and statistics

To install plugins: **Settings → Community plugins → Browse**.

## See Also

* [SuperMemory](../supermemory.md)
* [Process Inbox](process-inbox.md)
* [User Data Folder](../data-folder.md)
