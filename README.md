# Google Unity Packages (Unofficial)

Add this to your manifest.json:

```json
{
  "dependencies": {
  },
  "scopedRegistries": [
    {
      "name": "Google Unity Packages (Unofficial)",
      "url": "https://google-unity-packages.github.io/",
      "scopes": [
        "com.google"
      ]
    }
  ]
}
```

## How it works?

No magic involved, this repository just serves auto-generated package
manifests that redirect to .tgz files from
[Google Unity Package archive](https://developers.google.com/unity/archive)

The actual package manifests can be found in the
[gh-pages branch](https://github.com/google-unity-packages/google-unity-packages.github.io/tree/gh-pages)