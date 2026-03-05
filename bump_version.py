"""Bump Supervertaler.Trados version in plugin.xml (UTF-16 LE)."""
import os

PLUGIN_XML = os.path.join(os.path.dirname(__file__), "src", "Supervertaler.Trados", "Supervertaler.Trados.plugin.xml")

with open(PLUGIN_XML, "rb") as f:
    raw = f.read()

if raw[:2] == b'\xff\xfe':
    text = raw[2:].decode("utf-16-le")
else:
    text = raw.decode("utf-16-le")

# Only replace TermLens assembly version references (not Sdl.* DLL versions)
# The pattern is "TermLens, Version=1.1.0.0" and the plugin version="1.1.0.0"
old = "1.1.0.0"
new = "1.2.0.0"

count = text.count(old)
text = text.replace(old, new)
print(f"Replaced {count} occurrences of '{old}' with '{new}'")

with open(PLUGIN_XML, "wb") as f:
    f.write(b'\xff\xfe')
    f.write(text.encode("utf-16-le"))

print("Done.")
