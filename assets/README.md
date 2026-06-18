# TimeBarX Assets

`icon.svg` is the canonical source. Generate platform icons from it before
packaging.

## Windows `.ico`

```pwsh
magick icon.svg -define icon:auto-resize=256,128,64,48,32,16 icon.ico
```

ImageMagick handles the multi-resolution `.ico` Windows expects. Drop the
generated `icon.ico` next to this file before running `scripts/publish.ps1`.
