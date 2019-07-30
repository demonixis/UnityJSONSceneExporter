# UnityJSONSceneExporter
The goal of this project is to export an Unity scene into the JSON file format.
For now GameObjects and few components are supported.

## How does it works?
1. Copy the `ExportData.cs` and `SceneExport.cs` files into your project
2. Place the `SceneExport` script on a GameObject
3. Select a path, a filename
4. Click Export

All children of the GameObject that contains the script will be serialized into JSON and saved.
Now you can parse the JSON file with another game engine and reconstruct you scene.

## Exported Components

All exported component have a field called Enabled.

| Component | Fields |
|-----------|--------|
| MeshRenderer | Mesh, Materials |
| MeshFilter | Vertices, Indices, SubMeshes |
| Material | MainTexture Name, Offset, Scale |
| Collider | Min, Max, Radius |
| Light | Radius, Intensity, Type, Angle, Color, Shadows |
| Reflection Probe | Backed, Intensity, BoxSize, BoxMin, BoxMax, Resolution, Clip Planes |

### GameObject
A GameObject contains by default all components. You've to check if those components are valid or not.

#### Fields
- ID
- Name
- Parent
- IsStatic
- IsActive
- LocalPosition/Rotation/Scale
- Renderer / Collider / Light / ReflectionProbe

## License
This project is released under the MIT license.
