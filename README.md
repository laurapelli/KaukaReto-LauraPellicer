# Tank Battle

Prototipo en Unity de un duelo de tanques **jugador contra ordenador**. Los dos tanques se mueven libremente por el escenario y **disparan por turnos**: cuando uno dispara, no puede volver a hacerlo hasta que haya disparado el otro. Los proyectiles se **simulan con física** y cada impacto quita siempre la misma vida. Gana quien deja antes sin vida al rival.

## Cómo ejecutarlo

1. Abrir el proyecto con **Unity 6000.0.76f1**, con el pipeline **Built-in** (los materiales entregados usan shaders Built-in).
2. Paquetes necesarios (Window ▸ Package Manager):
   - **AI Navigation**, para el NavMesh de la IA.
   - **TextMeshPro**, para los textos de la interfaz (Unity lo pide al crear el primer texto: *Import TMP Essentials*).
3. Abrir la escena 'TankBattle'

## Controles

| Acción                   | Tecla                                                           |
|---|---|
| Avanzar / retroceder     | 'W' / 'S'                                                       |
| Girar el tanque          | 'A' / 'D'                                                       |
| Subir / bajar el cañón   | '↑' / '↓'                                                       |
| Disparar                 | Mantener 'Espacio' para cargar la fuerza y soltar para disparar |

La barra de fuerza sube y baja mientras se mantiene 'Espacio': hay que soltar en el momento justo.

---

## Cómo está organizado

```
TankController (abstracta)  | lo común: vida, cañón, disparo, Rigidbody
 - PlayerTankController    | teclado
 - AITankController        | inteligencia artificial (NavMesh + puntería)

BulletController            | la bala: hace daño al chocar y desaparece
TurnManager                 | alterna el permiso de disparo
CanvasController            | toda la interfaz
TankAudio                   | sonidos de disparo e impacto
```

| Script                | Va en                    | Responsabilidad |
|---|---|---|
| 'TankController'      | — (clase padre)          | Vida (100) y 'TakeDamage'. Elevación del cañón ('SetElevation', girando el hijo 'CannonPivot'). Disparo físico ('Fire') y conversión fuerza ↔ velocidad. Avisa con eventos: 'Fired', 'HealthChanged', 'Died' |
| 'PlayerTankController' |Tanque del jugador        | Lee el teclado. Mueve el tanque fijando la velocidad del Rigidbody, para que el motor de física resuelva los choques con rocas y paredes. Carga la fuerza y dispara |
| 'AITankController'    | Tanque enemigo           | Persigue al jugador con NavMesh, se mantiene a una distancia fija, apunta adelantándose a su movimiento y corrige su puntería con cada disparo |
| 'BulletController'    | Prefab de la bala        | Al chocar con cualquier cosa desaparece. Si es un tanque, le quita 25 de vida |
| 'TurnManager'         | GameController (empty)   | Da el permiso de disparo ('CanFire') a un tanque y se lo pasa al otro cuando dispara |
| 'CanvasController'    | CanvasController (empty) | Barras de vida, barra de fuerza, indicador de turno, panel de inicio y panel de fin de partida |
| 'TankAudio'           | Cada tanque              | Sonido al disparar y al recibir un impacto |


## Funcionamiento

### Turnos
- El **movimiento es libre** para los dos tanques en todo momento.
- El **disparo es por turnos**. El 'TurnManager' escucha el evento 'Fired' de cada tanque: cuando uno dispara, le quita el permiso y se lo da al otro. Empieza el jugador.
- La interfaz muestra de quién es el turno: **"You"** sobre rojo o **"Computer"** sobre azul.

### Disparo y proyectil
- La bala sale del 'FirePoint', un empty en la boca del cañón, en la dirección de su eje Z.
- La fuerza (0 a 1) se convierte en una velocidad de salida de **6 a 24 m/s**. A partir de ahí la trayectoria la calcula el **motor de física** (Rigidbody con gravedad y detección de colisiones continua). Nadie calcula la trayectoria.
- La bala ignora los colliders del tanque que la dispara, para no chocar consigo mismo al salir.
- **Daño fijo: 25 por impacto, sobre 100 de vida** 

### Inteligencia artificial
La IA vuelve a evaluar la situación cada 0,25 s:

1. **Movimiento:** calcula el punto que queda a *Keep Distance* (12 m) del jugador y va hacia él con NavMesh. Si el jugador se aleja, lo persigue; si se acerca, retrocede. Si se atasca, prueba a rodearlo por un lado.
2. **Puntería:**
   - Mantiene el cañón siempre a 35°, así no pierde tiempo antes de disparar.
   - Se adelanta al movimiento del jugador: estima el tiempo de vuelo y apunta a donde estará cuando llegue la bala.
3. **Aprendizaje iterativo** (lo pide el PDF: *"iteratively find the correct elevation and power"*):
   - **Fuerza:** Con cada impacto, la IA mide hasta dónde llegó realmente la bala y corrige *k*, lo que compensa la altura del cañón y otras diferencias de la escena.
   - **Dirección:** compara hacia dónde cayó la bala con hacia dónde apuntaba y corrige un sesgo de orientación.
4. **Dificultad:** se ajusta en el Inspector con *Direction Error*, *Power Error* y *Lead Factor*. Cuanto más error y menos predicción, más fácil.

### Interfaz (UI/UX)
- **Barras de vida:** azul para el jugador y roja para el enemigo. Bajan suavemente y muestran el número ('75 / 100').
- **Barra de fuerza:** aparece solo mientras se carga el disparo y muestra el porcentaje. Si no se asigna una en el Inspector, el 'CanvasController' la crea por código.
- **Indicador de turno**: "You" / "Computer".
- **Panel de inicio** con botón *Start*: hasta pulsarlo no se mueve nada. En la revancha no vuelve a aparecer.
- **Panel de fin de partida**: victoria o derrota, con botón para volver a jugar.
- **Minimapa** abajo a la izquierda: una segunda cámara ortográfica cenital pinta el escenario en una Render Texture que se muestra en una Raw Image.
- **Sonido** al disparar y al recibir un impacto, con una ligera variación de tono para que no suene repetitivo.

## Montaje de la escena

- **Tanques:** instancias de 'Tank_pref' a escala 0.25, con su 'Box Collider'. El hijo 'CannonPivot' (que contiene la torre) es el que se gira para elevar el cañón. Dentro va el 'FirePoint'.
- **NavMesh:** 'NavMeshSurface' en el suelo, horneado con *Use Geometry = Physics Colliders*. Los tanques se excluyen del horneado. El tanque del jugador lleva un 'NavMeshObstacle' para que la IA lo rodee.
- **Límites:** cuatro paredes con 'Box Collider' y sin Rigidbody (colliders estáticos).
- **Escena en Build Settings**, necesaria para el botón de volver a jugar.