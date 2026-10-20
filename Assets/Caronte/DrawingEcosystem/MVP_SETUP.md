# Drawing Ecosystem MVP Setup

Este prototipo está preparado en `Assets/Caronte/DrawingEcosystem/` y no sobrescribe ninguna escena existente.

## Configuración detectada

- Unity: `6000.3.10f1`.
- Render Pipeline: URP `17.3.0`.
- Sistema de entrada activo: Input System nuevo (`activeInputHandler: 1`).
- Assets previos relacionados: no existían sistemas de captura, ecosistema o vídeo; sólo `SampleScene`, `Assets/Settings`, `InputSystem_Actions` y scripts tutorial `Readme`.

## Crear la escena MVP automáticamente

1. Abre el proyecto en Unity y espera a que compile.
2. Ejecuta el menú `Tools > Caronte > Drawing Ecosystem > Create MVP Scene`.
3. Unity creará `Assets/Caronte/DrawingEcosystem/Scenes/DrawingEcosystem_MVP.unity`.
4. La escena incluye:
   - `DrawingEcosystem_Managers` con todos los componentes conectados.
   - `Main Camera`, luz, suelo, plano de preview webcam y dos planos de vídeo de fondo.
   - Cuatro prefabs placeholder.
   - Cuatro `DrawingTypeDefinition` configurados al 25% inicial.
   - Máscara blanca placeholder legible.

## Crear GameObjects manualmente si no usas el builder

1. Crea una escena nueva llamada `DrawingEcosystem_MVP` en `Assets/Caronte/DrawingEcosystem/Scenes/`.
2. Crea un GameObject `DrawingEcosystem_Managers`.
3. Añade estos componentes al mismo GameObject:
   - `DrawingCaptureService`.
   - `TextureProcessor`.
   - `MockTemplateDetector`.
   - `SpawnManager`.
   - `EcosystemManager`.
   - `BackgroundVideoController`.
   - `DrawingGameController`.
   - `DebugGameUI`.
4. Crea un GameObject vacío `SpawnedDrawings` y asígnalo como `Spawn Parent` en `SpawnManager`.
5. Crea un plano/quad para preview de webcam y asígnalo a `DrawingCaptureService > Preview Renderer`.
6. Crea dos quads detrás de los modelos 3D para vídeo de fondo.
7. Añade un `VideoPlayer` a cada quad y asígnalos en `BackgroundVideoController` como `Player A` y `Player B`.
8. Asigna los renderers de esos dos quads como `Renderer A` y `Renderer B`.
9. En `DrawingGameController`, conecta `DrawingCaptureService`, `TextureProcessor`, `MockTemplateDetector`, `SpawnManager`, `EcosystemManager` y `BackgroundVideoController`.
10. En `DebugGameUI`, conecta `DrawingGameController`, `DrawingCaptureService`, `MockTemplateDetector`, `EcosystemManager` y `BackgroundVideoController`.

## Crear los cuatro ScriptableObjects

Crea cuatro assets desde `Create > Caronte > Drawing Ecosystem > Drawing Type Definition`:

1. `DT_Mycorrhiza`
   - `Type`: `Mycorrhiza`.
   - `Display Name`: `Micorriza`.
   - `Prefab`: prefab de micorriza.
2. `DT_Fungus`
   - `Type`: `Fungus`.
   - `Display Name`: `Hongo`.
   - `Prefab`: prefab de hongo.
3. `DT_Bacteria`
   - `Type`: `Bacteria`.
   - `Display Name`: `Bacteria`.
   - `Prefab`: prefab de bacteria.
4. `DT_Nematode`
   - `Type`: `Nematode`.
   - `Display Name`: `Nematodo`.
   - `Prefab`: prefab de nematodo.

Para cada definición:

- `Optional Mask`: asigna la máscara PNG correspondiente o déjalo vacío para no aplicar máscara.
- `Crop Region`: configura el rectángulo fijo de captura en píxeles de la webcam.
- `Texture Property`: usa `_BaseMap` para materiales URP/Lit o URP/Unlit; usa `_MainTex` si el shader lo requiere.
- `Spawn Center`, `Spawn Area` y `Uniform Scale Range`: ajustan aparición aleatoria.

Después, asigna los cuatro assets en `SpawnManager > Definitions`.

## Asignar prefabs

- Los prefabs reales deben tener UVs preparadas para recibir la textura capturada.
- El sistema aplica la textura con `MaterialPropertyBlock`, por lo que no modifica el material compartido del prefab.
- Si un prefab tiene varios renderers hijos, `SpawnManager` aplica la textura a todos por defecto.

## Asignar máscaras

- Las máscaras deben ser `Texture2D` con `Read/Write` activado.
- El alfa final se calcula con alfa y luminancia de la máscara.
- Una máscara blanca equivale a no enmascarar.

## Asignar los cinco vídeos

1. Importa los cinco `VideoClip` de la dehesa castellana.
2. Selecciona `DrawingEcosystem_Managers`.
3. En `BackgroundVideoController > Background Clips`, asigna exactamente cinco clips en orden de peor a mejor estado.
4. Ajusta `Fade Duration` según la suavidad deseada.
5. Los vídeos se reproducen en loop, se preparan antes del fundido y se renderizan en quads 3D detrás de los modelos.

## Configurar puntuación y fondos

- En `EcosystemManager > Target Proportions`, deja inicialmente los cuatro tipos a `0.25`.
- En `Background Score Thresholds`, configura los umbrales de los cinco fondos.
- Si no hay organismos, la puntuación es `0` y el fondo calculado es `1`.

## Probar captura

1. Entra en Play Mode.
2. `DrawingCaptureService` seleccionará automáticamente la primera cámara cuyo nombre contenga `IPEVO`.
3. Si no hay IPEVO, usará la primera webcam disponible.
4. En la UI debug, puedes refrescar y elegir manualmente otra webcam.
5. Selecciona `Micorriza`, `Hongo`, `Bacteria` o `Nematodo`.
6. Pulsa `Capturar dibujo`.
7. Se captura el frame, se recorta, se aplica máscara opcional, se instancia el prefab, se aplica textura individual y se actualizan contadores/proporciones/puntuación.
8. Usa los botones `1` a `5` de `Forzar fondo` para probar los cinco estados de vídeo.

## Sustituciones futuras previstas

- `QRTemplateDetector`: implementar `ITemplateDetector` y conectarlo en lugar de `MockTemplateDetector`.
- `ArduinoSerialController`: emitir evento de captura compatible con `ICaptureTrigger` o llamar a `DrawingGameController.CaptureSelectedTemplate()`.
- Iluminación física: componente separado que escuche cambios de `EcosystemManager` o estados de fondo.
- Botón físico de captura: adaptador que dispare el mismo flujo que `DebugGameUI.RequestCapture()`.

## Pendiente para producción

- Sustituir prefabs placeholder por modelos con UV final.
- Medir la región `Crop Region` exacta con la IPEVO V4K en la base física.
- Asignar máscaras definitivas por plantilla.
- Asignar los cinco vídeos definitivos.
- Ajustar escala/posición de cámara, quads de fondo y zona de aparición para la instalación real.
