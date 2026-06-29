# Changelog

All notable changes to this package are documented here.

## [0.1.0] - unreleased

### Added
- Package scaffold: UPM `package.json`, Runtime + Editor assembly definitions, MIT license, README.
- Runtime layered character system **migrated** out of the Ultima4_2d game (namespace `Lpc`):
  `LpcCharacter`, `LpcAnimator`, `LpcCharacterBuilder`, `LpcCharacterSpawner`, `LpcLayerSet`,
  `LpcRecipe`, plus a new `ILpcMotion` interface that decouples the animator from any specific
  movement controller (`Facing`/`Walking`). GUIDs preserved so consuming projects keep their
  scene/asset references.
- (planned) Selective catalog importer (manifest + copy + auto-slicing AssetPostprocessor)
  and palette recolor.
