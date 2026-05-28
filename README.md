# VectorKit

Figma-style vector graphics plugin for Unity 6+. SDF-based rendering, multiple fills and strokes per shape, layered effects, all 16 Figma blend modes, and SVG import — in both Canvas (uGUI) and World Space.

## Requirements

- Unity 6000.0+
- com.unity.ugui 2.0.0+

## Installation

Copy the `VectorKit` folder into your project's `Assets` directory. The plugin uses two assembly definitions:

- `VectorKit.Runtime` — runtime components and rendering
- `VectorKit.Editor` — inspector drawers, scene handles, overlays, and menus

## Quick Start

**Via menu:**  
`GameObject → Vector → Rectangle` (or Ellipse, Polygon, Star, Line, Arc, Path, Group, Frame)

This creates a `VectorShapeUI` component inside the nearest Canvas, ready to edit in the Inspector.

**Manual setup:**
```csharp
var go = new GameObject("MyShape");
go.transform.SetParent(canvasTransform, false);

var ui = go.AddComponent<VectorShapeUI>();
ui.Shape = new RectangleShape { CornerRadius = new Vector4(8, 8, 8, 8) };
ui.Fills.Add(new FillLayer
{
    Fill = new LinearGradientFill
    {
        Gradient = myGradient,
        Angle = 90f
    }
});
```

## Shapes

| Type | Key Parameters |
|------|---------------|
| `RectangleShape` | `CornerRadius` (per-corner Vector4), `CornerSmoothing` |
| `EllipseShape` | — |
| `PolygonShape` | `Sides`, `Rounding`, `Rotation` |
| `StarShape` | `Points`, `Ratio`, `OuterRounding`, `InnerRounding` |
| `LineShape` | `Start`, `End`, `Width`, `Cap` |
| `ArcShape` | `InnerRadius`, `StartAngle`, `EndAngle` |
| `CapsuleShape` | — |
| `TriangleShape` | `Rounding` |
| `HeartShape` | — |
| `PathShape` | `Points` (List\<PathPoint\>), `Closed` |
| `BooleanShape` | `Operations` (Union / Subtract / Intersect / Xor) |

All shapes share `Scale`, `Pivot`, `EdgeSoftness`, and `InternalPadding`.

## Fills

Each shape supports multiple fill layers stacked in order. Every layer has its own `BlendMode` and `Opacity`.

| Fill Type | Key Parameters |
|-----------|---------------|
| `SolidFill` | `Color` |
| `LinearGradientFill` | `Gradient`, `Angle`, `Offset`, `Scale` |
| `RadialGradientFill` | `Gradient`, `Center`, `Radius` |
| `ConicGradientFill` | `Gradient`, `Center`, `StartAngle` |
| `ImageFill` | `Texture`, `Tiling`, `Offset`, `FitMode` |

## Strokes

Each shape also supports multiple stroke layers. Key parameters: `Width`, `Alignment` (Inside / Center / Outside), `Dash`, `Gap`, `Cap`, `Joint`, plus a `FillDefinition` (any fill type).

## Effects

| Effect | Key Parameters |
|--------|---------------|
| `DropShadowEffect` | `Offset`, `Blur`, `Spread`, `Fill` |
| `InnerShadowEffect` | `Offset`, `Blur`, `Spread`, `Fill` |
| `OuterGlowEffect` | `Blur`, `Spread`, `Fill` |
| `InnerGlowEffect` | `Blur`, `Spread`, `Fill` |
| `GaussianBlurEffect` | `Radius` |
| `BevelEffect` | `Distance`, `Angle`, `HighlightAlpha`, `ShadowAlpha` |

Draw order: `DropShadow → OuterGlow → Fills → InnerShadow → InnerGlow → Strokes → Bevel`

## Blend Modes

16 Figma-compatible blend modes: Normal, Multiply, Screen, Overlay, Darken, Lighten, Color Dodge, Color Burn, Hard Light, Soft Light, Difference, Exclusion, Hue, Saturation, Color, Luminosity.

Per-fill and per-stroke blend modes work via standard GPU alpha compositing. Non-Normal blend modes on a group require a `VectorGroup` component (see below).

## Components

### VectorShapeUI
`MaskableGraphic` + `ICanvasRaycastFilter` — renders inside a Canvas. Raycasting is pixel-perfect: clicks outside the SDF shape boundary are ignored.

```csharp
[SerializeReference] public ShapeDefinition  Shape;
public List<FillLayer>   Fills;
public List<StrokeLayer> Strokes;
public List<VectorEffect> Effects;
[Range(0,1)] public float ShapeOpacity;
```

### VectorShapeWorld
`MonoBehaviour` + `MeshFilter` + `MeshRenderer` — renders in World Space without a Canvas.

```csharp
public Vector2 Size;
public Color   Tint;
// + same Shape / Fills / Strokes / Effects fields
```

Call `Rebuild()` after changing properties at runtime.

### VectorGroup
Groups shapes for blend mode isolation and opacity control. Uses `CanvasGroup` for opacity. Non-Normal blend modes on a group require a URP RenderFeature for full RT compositing (see `VectorBlend.shader`).

### VectorFrame
Clipping frame — `MaskableGraphic` + `Mask`. Add a Unity `Mask` component to the same GameObject to clip children to the frame shape. Supports a background `FillLayer`.

## SVG Import

Rename your `.svg` file to `.vksvg` and drop it into the Project window. VectorKit registers a `ScriptedImporter` for `.vksvg` (the `.svg` extension is already claimed by Unity's built-in VectorGraphics module — both cannot coexist).

The importer supports: `<path>`, `<rect>`, `<circle>`, `<ellipse>`, `<line>`, `<polyline>`, `<polygon>`, `<g>`. All SVG path commands are handled (M, L, H, V, C, S, Q, T, A, Z in both absolute and relative form). Arc segments are converted to cubic Bézier curves.

Import settings are exposed in the Inspector:

| Property | Default | Description |
|----------|---------|-------------|
| Pixels Per Unit | 1 | Scale factor for World Space import |
| Use UI Canvas | true | Creates `VectorShapeUI` hierarchy; false creates `VectorShapeWorld` |

## Editor Tools

### Inspector
Custom inspector for `VectorShapeUI` and `VectorShapeWorld` with:
- Shape type dropdown (hot-swap without losing other properties)
- Reorderable fill / stroke / effect lists with per-layer enable toggles

### Scene Handles
Interactive handles appear in the Scene View when a shape is selected:
- **Rectangle** — drag corner radius handles
- **Star** — drag inner ratio handle
- **Path** — move points and Bézier control handles
- **Gradient fills** — drag offset, angle/scale, and radius anchors

### Overlays (Unity 6 Overlay API)
Three overlays available from `Overlays` menu in the Scene View:
- **VectorKit Tools** — quick-create buttons for Rectangle, Ellipse, Path, Star, Arc
- **VectorKit Layers** — lists all `VectorShapeUI` in the scene with visibility toggles
- **VectorKit Properties** — opacity slider and shape type label for the selected shape

### Editor Window
`Tools → Vector Kit → Open Editor` — floating inspector that embeds the full shape inspector and follows the current selection.

### Menu
`Tools → Vector Kit → Clear Material Pool` — flushes the material pool, gradient atlas, and RT pool (useful after bulk edits).

## Architecture Notes

**SDF rendering** — all shapes are rendered as screen-aligned quads. The signed distance function is evaluated per-pixel in the fragment shader (`SDFLibrary.hlsl`). Sub-pixel anti-aliasing is computed from the SDF gradient.

**Material pooling** — `VectorMaterialManager` shares materials across identical shapes using `VectorShaderState` as a key (hash + equality). Hundreds of identical shapes produce 1–2 draw calls.

**Gradient atlas** — `GradientAtlas` packs all gradients into a single `Texture2D` (256 px wide, dynamic height 256→2048). Each gradient occupies 3 rows for safe bilinear sampling. Rows are ref-counted and reused.

**[SerializeReference] polymorphism** — `ShapeDefinition`, `FillDefinition`, and `VectorEffect` use `[SerializeReference]` for type-safe polymorphic serialization. The inspector draws child properties via `SerializedProperty.NextVisible()` — never `PropertyField` on the managed-reference field itself.

## Shaders

| Shader | Purpose |
|--------|---------|
| `VectorKit/Shape` | Main UI shader (Canvas, stencil-aware) |
| `VectorKit/ShapeWorld` | World Space variant (standard depth) |
| `VectorKit/Blend` | RT compositing for VectorGroup blend modes |
| `VectorKit/SoftMask` | Soft mask variant |

## DOTween Integration

If DOTween is installed (`#if DOTWEEN`), extension methods are available:

```csharp
shape.DOShapeOpacity(0f, 0.3f);
shape.DOFillColor(0, Color.red, 0.5f);
shape.DOStrokeWidth(0, 4f, 0.3f);
shape.DOCornerRadius(new Vector4(16,16,16,16), 0.4f);
```
