# **Arquitectura de Software y Algoritmia Avanzada para Herramientas de Destiny 2: Un Análisis Técnico Profundo de Destiny Item Manager y D2ArmorPicker**

## **1\. Introducción: El Ecosistema de Desarrollo sobre la API de Bungie**

El desarrollo de aplicaciones complementarias para *Destiny 2* representa uno de los desafíos más complejos y gratificantes en el ámbito de la ingeniería de software orientada a videojuegos. A diferencia de muchos otros títulos que ofrecen APIs de solo lectura para tablas de clasificación, Bungie proporciona una plataforma bidireccional masiva que permite la manipulación del estado del inventario del jugador en tiempo real. Para un desarrollador que aspira a crear una herramienta unificada que combine la gestión de inventario de **Destiny Item Manager (DIM)** y la optimización combinatoria de **D2ArmorPicker (D2AP)**, no basta con conocer los endpoints de la API; es imperativo comprender las arquitecturas de software subyacentes que permiten a estas aplicaciones manejar gigabytes de definiciones estáticas y miles de permutaciones matemáticas sin comprometer el rendimiento del navegador.

Este informe disecciona la arquitectura monolítica basada en estados de DIM y el motor de optimización matemática de D2AP. A través del análisis de sus repositorios de código abierto, examinaremos cómo DIM transforma respuestas JSON crudas en objetos interactivos mediante patrones de fábrica en TypeScript, y cómo D2AP emplea algoritmos de ramificación y poda (branch and bound) para resolver problemas de optimización de enteros. El objetivo es proporcionar una hoja de ruta técnica exhaustiva para replicar y fusionar estas funcionalidades en una nueva aplicación de alto rendimiento.

## ---

**2\. Fundamentos de Datos: La Capa de Abstracción de Bungie**

Antes de analizar el código fuente de las herramientas, es crucial establecer cómo se estructuran los datos que estas aplicaciones consumen. Tanto DIM como D2AP actúan como "clientes inteligentes" que deben sintetizar dos flujos de datos dispares: el **Manifiesto** (definiciones estáticas) y el **Perfil** (datos dinámicos de usuario).

### **2.1 La Arquitectura del Manifiesto de Destiny**

El Manifiesto es la columna vertebral de cualquier aplicación de *Destiny 2*. Es una base de datos SQLite masiva que contiene la definición inmutable de cada entidad en el juego. Cuando la API del perfil de un usuario indica que posee el ítem con hash 3520001075, no proporciona el nombre, el icono ni las estadísticas base de ese ítem. Es responsabilidad de la aplicación buscar ese hash en la tabla DestinyInventoryItemDefinition del Manifiesto para renderizar la información correcta.1

En el desarrollo de aplicaciones web modernas para Destiny, no se consulta la base de datos SQLite directamente en cada renderizado debido a la latencia de E/S. Herramientas como DIM y D2AP implementan una capa de preprocesamiento o utilizan bibliotecas que descargan, descomprimen y almacenan estas definiciones en **IndexedDB** dentro del navegador del usuario. Esto permite búsquedas O(1) instantáneas mediante índices hash, lo cual es vital cuando se renderizan cientos de ítems en una cuadrícula de inventario.3

La estructura de datos de una definición de ítem (DestinyInventoryItemDefinition) es compleja y recursiva. Contiene punteros a otras tablas del manifiesto, como DestinyStatDefinition (para entender qué significa la estadística "Recuperación") y DestinySocketDefinition (para entender qué mods se pueden insertar). El dominio de estas relaciones relacionales es el primer paso para construir cualquier herramienta.5

### **2.2 Autenticación y Seguridad OAuth 2.0**

El acceso al inventario de un jugador requiere una implementación robusta de OAuth 2.0.

* **Scopes Críticos:** Para una aplicación que aspira a gestionar inventario y crear builds, es obligatorio solicitar los permisos ReadDestiny2Data (para ver ítems) y MoveEquipDestinyItems (para moverlos y equiparlos). D2ArmorPicker, por ejemplo, utiliza estos scopes para leer las armaduras disponibles y equipar la configuración optimizada resultante.5  
* **Ciclo de Vida del Token:** La aplicación debe gestionar tokens de acceso de corta duración y tokens de refresco de larga duración. DIM maneja esto mediante interceptores HTTP que detectan errores 401 Unauthorized, pausan la cola de peticiones, refrescan el token y reintentan la operación original sin que el usuario perciba la interrupción.

### **2.3 Librerías de Enlace (Wrappers)**

Un hallazgo fundamental en la investigación del código fuente de DIM es su dependencia y mantenimiento de la librería bungie-api-ts. En lugar de escribir peticiones fetch manuales y definir interfaces TypeScript para cada respuesta (lo cual sería propenso a errores dado el tamaño de la API), DIM utiliza esta librería que genera automáticamente los tipos basándose en la documentación oficial de Bungie. Para su nueva aplicación, es altamente recomendable utilizar bungie-api-ts para garantizar la seguridad de tipos en todo el flujo de datos, desde la respuesta de la red hasta el componente de la interfaz de usuario.8

## ---

**3\. Análisis Profundo de Destiny Item Manager (DIM): Arquitectura de Inventario**

DIM es, en esencia, un sistema operativo para los ítems de Destiny. Su arquitectura está diseñada para mantener una "copia local" del estado del juego, permitiendo operaciones complejas que la API de Bungie no soporta nativamente, como "Mover a otro personaje" (que implica múltiples llamadas a la API) o "Filtrar por Tier".

### **3.1 Stack Tecnológico y Estructura del Proyecto**

El repositorio de DIM revela una arquitectura de **Single Page Application (SPA)** construida sobre un stack moderno y robusto, diseñado para escalabilidad y mantenimiento a largo plazo.

| Componente | Tecnología | Justificación Técnica |
| :---- | :---- | :---- |
| **Framework UI** | React | Componentización y Virtual DOM para renderizar miles de ítems eficientemente. |
| **Gestión de Estado** | Redux & Redux-Thunk | Gestión predecible del estado global (inventario, configuraciones, descargas) y manejo de efectos secundarios asíncronos (llamadas API). |
| **Lenguaje** | TypeScript | Tipado estático estricto, esencial para manejar los miles de propiedades de los objetos de Bungie. |
| **Build System** | Webpack / RSPack | Empaquetado modular y optimización de carga (lazy loading).10 |
| **Estilos** | CSS Modules / SCSS | Estilos encapsulados para evitar colisiones en una aplicación masiva.11 |
| **Persistencia** | IndexedDB (idb-keyval) | Almacenamiento local del Manifiesto y configuraciones de usuario para funcionamiento offline/PWA.4 |

#### **Estructura de Directorios Clave (src/app)**

La organización del código en src/app es modular, separando las responsabilidades lógicas de las visuales:

* src/app/inventory: El núcleo de la aplicación. Contiene la lógica para procesar, almacenar y visualizar los ítems.  
  * d2-stores.ts: Este archivo es el "cerebro" del inventario. Contiene la lógica para convertir la respuesta cruda de GetProfile en el modelo interno de DIM (DimStore y DimItem).12  
  * store/stats.ts: Lógica matemática para calcular las estadísticas finales de un ítem, sumando base, masterwork y mods.14  
* src/app/loadout: Contiene la lógica del Optimizador de Loadouts y el almacenamiento de configuraciones guardadas.  
* src/app/dim-api: Cliente para la API propietaria de DIM, utilizada para sincronizar etiquetas (tags) y notas entre dispositivos, una funcionalidad que la API de Bungie no ofrece.15

### **3.2 El Modelo de Datos: De JSON Crudo a DimItem**

La magia de DIM reside en cómo transforma los datos. La API de Bungie devuelve los datos fragmentados: los ítems están en una lista, las estadísticas en otra, y los perks en otra (componentes ItemInstances, ItemStats, ItemSockets).

#### **3.2.1 El Patrón de Fábrica (ItemFactory)**

DIM implementa un patrón de fábrica (Factory Pattern) para unificar estos fragmentos. Dentro de d2-stores.ts y archivos auxiliares, existe una tubería de procesamiento que se ejecuta cada vez que se actualiza el inventario:

1. **Ingesta:** Se reciben los componentes del perfil (ProfileInventories, ProfileCurrencies, ItemComponents).  
2. **Hidratación:** Para cada hash de ítem, se consulta la DestinyInventoryItemDefinition en el Manifiesto cargado en memoria.  
3. **Construcción del Objeto DimItem:** Se crea un objeto unificado que contiene:  
   * Datos visuales (nombre, icono) del Manifiesto.  
   * Datos de instancia (id único, nivel de luz, estadística específica) del Perfil.  
   * Datos derivados (etiquetas del usuario, notas, cálculos de estadísticas totales).  
4. **Clasificación:** El ítem se asigna a una "Tienda" (DimStore), que representa a un personaje específico (Titán, Cazador, Hechicero) o al Depósito (Vault).17

Este proceso de normalización es lo que permite que la interfaz de usuario sea tan rápida. Los componentes de React no necesitan hacer cálculos ni búsquedas; simplemente reciben un objeto DimItem completo y lo renderizan.

### **3.3 Lógica Algorítmica: "Smart Moves" (Movimientos Inteligentes)**

Una de las funcionalidades más complejas y valoradas de DIM es "Smart Moves". La API de Bungie tiene una limitación estricta: los ítems solo pueden moverse desde un personaje al Depósito, o desde el Depósito a un personaje. No existe un "Mover de Personaje A a Personaje B" directo. Además, los inventarios tienen un tamaño fijo (9 espacios por slot).

El algoritmo de Smart Moves, ubicado conceptualmente en la lógica de inventory, resuelve este problema de grafos y colas de la siguiente manera 19:

1. **Intención:** El usuario arrastra un Cañón de Mano del Cazador al Titán.  
2. **Análisis de Estado:**  
   * ¿Está el ítem equipado en el Cazador? Si sí, se debe enviar primero una orden de EquipItem para ponerle otro arma y liberar el ítem deseado.  
   * ¿Está lleno el Depósito? Si el Depósito tiene 600/600 ítems, el movimiento fallará.  
   * ¿Está lleno el slot de Cinéticas del Titán? Si tiene 9 armas, no puede recibir una más.  
3. **Planificación (Pathfinding):**  
   * El algoritmo calcula una cadena de operaciones. Si el Depósito está lleno, busca un ítem "candidato a mover" en el Depósito (basado en criterios de "basura" o antigüedad) y genera una orden para moverlo a cualquier personaje con espacio libre.  
   * Si el Titán está lleno, selecciona un arma del Titán para moverla al Depósito y hacer espacio.  
4. **Ejecución en Cola:** Se genera una secuencia de promesas (Promises) de JavaScript:  
   * Op1: Move(Item\_X, Vault \-\> Warlock) (Hacer espacio en Vault)  
   * Op2: Move(Item\_Y, Titan \-\> Vault) (Hacer espacio en Titán)  
   * Op3: Move(Target\_Item, Hunter \-\> Vault)  
   * Op4: Move(Target\_Item, Vault \-\> Titan)  
5. **Manejo de Errores y Throttling:** Cada paso de la cola monitorea la respuesta. Si Bungie devuelve un error de Throttling (demasiadas peticiones), DIM pausa la cola, espera unos segundos (backoff exponencial) y reintenta. Esto es crítico para evitar baneos temporales de la API.5

### **3.4 El Motor de Búsqueda y Filtrado**

La barra de búsqueda de DIM no es un simple filtro de texto; es un intérprete de lenguaje específico de dominio (DSL).

* **Lexer/Parser:** Cuando el usuario escribe is:sniper perk:"snapshot" \-is:exotic, DIM analiza esta cadena y la convierte en un árbol de sintaxis abstracta (AST).  
* **Evaluación:** Cada ítem en el inventario pasa por una función de evaluación contra este árbol.  
* **Metadatos:** El filtro accede a propiedades profundas, como item.tags (gestión local) o item.stats.base (datos del manifiesto). La implementación de esto suele residir en módulos de utilidad de búsqueda dentro de src/app/search.22

## ---

**4\. Análisis Profundo de D2ArmorPicker (D2AP): El Optimizador Matemático**

Mientras que DIM es un gestor de estado generalista, D2ArmorPicker es una herramienta de cálculo intensivo especializada. Su único propósito es resolver el problema de optimización de estadísticas, que matemáticamente es una variación del **Problema de la Mochila (Knapsack Problem)** o, más específicamente, un problema de **Suma de Subconjuntos (Subset Sum)** multidimensional.

### **4.1 Stack Tecnológico y Arquitectura**

D2AP difiere significativamente en su elección tecnológica, optimizada para el cálculo reactivo.

* **Framework:** **Angular**. La estructura del proyecto, visible en los archivos tsconfig.app.json y package.json, confirma el uso de este framework, conocido por su robusto sistema de inyección de dependencias y servicios.7  
* **Manejo de Datos Asíncronos:** **RxJS**. D2AP utiliza programación reactiva masivamente. Los cambios en la selección del usuario (ej. "quiero 100 de Recuperación") se emiten como flujos de datos (Observables) que disparan el re-cálculo del optimizador.  
* **Rendimiento:** **Web Workers**. Debido a que el cálculo de combinaciones puede bloquear el hilo principal de la UI, D2AP delega la fuerza bruta matemática a hilos de fondo.25

### **4.2 El Algoritmo del Solucionador (The Solver)**

El corazón de D2AP es su algoritmo para encontrar la combinación perfecta de armadura. Un usuario veterano puede tener 50 cascos, 50 guanteletes, 50 pecheras y 50 botas. Esto genera un espacio de búsqueda de $50^4 \= 6,250,000$ combinaciones base, sin contar los mods y fragmentos.

#### **4.2.1 La Ecuación Matemática del Build**

Para cada permutación válida de armadura ($H, G, C, L$), el sistema debe verificar si se cumplen las restricciones del usuario. La ecuación para una estadística total ($S\_{total}$) es:

$$S\_{total} \= \\sum\_{i \\in \\{H,G,C,L,CI\\}} (Base\_i \+ MW\_i) \+ \\sum\_{j=1}^{5} Mod\_j \+ \\sum\_{k \\in Fragments} Frag\_k \+ Artifice$$  
Donde:

* $Base\_i$: Estadística base de la pieza de armadura $i$.  
* $MW\_i$: Bono de Masterwork (+2 si la pieza está mejorada al máximo).  
* $Mod\_j$: Valor de los mods de estadísticas insertados (+5 o \+10).  
* $Frag\_k$: Bonificaciones o penalizaciones de los fragmentos de subclase seleccionados (+10, \-10, etc.).  
* $Artifice$: Bono flexible (+3) si se utiliza armadura artífice.26

La restricción clave que el algoritmo debe satisfacer es la integridad de los Tiers (Niveles):

$$\\lfloor \\frac{S\_{total}}{10} \\rfloor \\ge Tier\_{deseado}$$

#### **4.2.2 Estrategias de Optimización y Poda**

Para no iterar sobre millones de combinaciones inútiles, D2AP implementa técnicas de optimización avanzadas:

1. **Clustering (Agrupamiento):** Si tienes tres cascos con la misma distribución de estadísticas (ej. 20 Movilidad, 10 Recuperación), el algoritmo los trata como una única "clase de equivalencia". No calcula permutaciones para los tres, sino para uno representativo. Esto reduce drásticamente el valor de $N$ en $N^4$.27  
2. **Branch and Bound (Ramificación y Poda):** El algoritmo ordena las piezas por estadística total. Si al sumar el mejor Casco y los mejores Guanteletes, la suma parcial ya hace imposible alcanzar el Tier 100 deseado (incluso con la mejor Pechera y Botas teóricas), esa rama del árbol de decisión se "poda" (descarta) inmediatamente, ahorrando miles de ciclos de CPU.  
3. **Pre-cálculo:** Es probable que D2AP calcule sumas parciales, por ejemplo, combinando todas las (Cabezas \+ Brazos) y todas las (Pechos \+ Piernas) en dos grandes listas, y luego busque pares entre estas dos listas que satisfagan los requisitos, convirtiendo un problema $O(N^4)$ en algo más cercano a $O(N^2 \\log N)$ (algoritmo *Meet-in-the-middle*).

#### **4.2.3 Lógica de "Zero Waste" (Desperdicio Cero)**

Una funcionalidad destacada de D2AP es minimizar los puntos desperdiciados (ej. tener 79 en una estadística es igual a tener 70; los 9 puntos son "desperdicio").  
El algoritmo añade una función de coste a minimizar:

$$Coste \= \\sum\_{s \\in Stats} (S\_{total, s} \\pmod{10})$$

En el modo estricto de "Zero Waste", esta función se convierte en una restricción dura: $(S\_{total, s} \\pmod{10}) \== 0$ para todas las estadísticas seleccionadas. Esto requiere un manejo inteligente de los mods de armadura artífice (+3), tratándolos como variables libres que el solver debe asignar en el slot óptimo para redondear las cifras.28

### **4.3 Integración con Bungie API**

A diferencia de DIM, D2AP no necesita mover ítems para hacer sus cálculos. Su interacción con la API es principalmente de lectura (GetProfile para obtener el inventario) y escritura selectiva (EquipLoadout o generación de enlaces para DIM). El código de D2AP se centra en la manipulación de arrays de números en memoria (las estadísticas) más que en la gestión de objetos de inventario complejos.7

## ---

**5\. Guía de Implementación: Integrando lo Mejor de Ambos Mundos**

Para desarrollar una aplicación que combine la gestión robusta de inventario de DIM con la potencia de cálculo de D2AP, se propone la siguiente arquitectura híbrida.

### **5.1 Fase 1: Arquitectura y Autenticación**

* **Recomendación de Stack:** Utilice **Next.js (React)**. React le permite utilizar el vasto ecosistema de componentes de DIM, mientras que Next.js facilita el enrutamiento y la optimización del rendimiento. Use **TypeScript** obligatoriamente.  
* **Librería Core:** Instale bungie-api-ts inmediatamente. No intente escribir las definiciones de tipos manualmente.  
* **Gestión del Manifiesto:** Implemente un servicio (Service Worker) que descargue el Manifiesto, lo descomprima usando una librería WASM de SQLite (como sql.js) y almacene las tablas críticas (InventoryItem, Stat, PlugSet) en **IndexedDB**. Librerías como @d2api/manifest-web pueden automatizar esto.4

### **5.2 Fase 2: Desarrollo del Inventario (Modelo DIM)**

Debe replicar el pipeline de procesamiento de datos de DIM:

1. **Store Factory:** Cree una función que tome el itemHash y el itemInstanceId.  
2. **Hydration:** Fusione los datos estáticos (nombre, tier, tipo) con los dinámicos (nivel de luz, masterwork).  
3. **UI Component:** Diseñe un componente InventoryTile que acepte este objeto fusionado. Use librerías como react-dnd (React Drag and Drop) para manejar las interacciones de arrastrar y soltar, mapeando los eventos onDrop a la lógica de transferencia.16  
4. **Action Queue:** Implemente una cola de acciones para las transferencias. No dispare la llamada a la API inmediatamente al soltar el ítem. Agréguela a una cola que verifique el estado de "ocupado" del destino y genere los movimientos intermedios necesarios (la lógica de "Smart Moves").

### **5.3 Fase 3: Motor de Builds (Modelo D2AP)**

Para el creador de builds, separe la lógica del hilo principal de React para evitar congelar la interfaz.

1. **Web Worker Solver:** Escriba el algoritmo de permutación en un archivo separado para ejecutarlo como Web Worker.  
2. **Input de Datos:** El Worker debe recibir una versión simplificada del inventario: solo arrays de objetos que contengan { id, hash, stats array, type }. No envíe los objetos pesados de la UI.  
3. **Algoritmo:** Implemente el agrupamiento (Clustering) antes de permutar. Agrupe ítems idénticos estadísticamente.  
4. **Salida:** El Worker devuelve una lista de configuraciones (IDs de ítems \+ Mods necesarios). La UI principal recibe esto y lo muestra en una tabla.

### **5.4 Integración de Funcionalidades**

La verdadera potencia surge al conectar ambos módulos:

* **Flujo:** El usuario crea una build en la pestaña "Optimizador". Al hacer clic en "Equipar", la aplicación no solo llama a la API de equipar, sino que invoca al gestor de inventario para que ejecute la lógica de "Smart Moves" si los ítems están en otros personajes o en el depósito.  
* **Loadouts:** Guarde los resultados del optimizador como objetos JSON que el gestor de inventario pueda leer y aplicar posteriormente.

## **6\. Conclusión**

La creación de una herramienta para *Destiny 2* que rivalice con DIM y D2ArmorPicker es un ejercicio de arquitectura de software avanzada. Requiere una gestión meticulosa del estado global para el inventario, inspirada en los patrones Redux de DIM, y una implementación eficiente de algoritmos matemáticos en Web Workers, siguiendo el modelo de D2AP. La clave del éxito no reside solo en llamar a la API de Bungie, sino en cómo se procesan, almacenan y manipulan esos datos localmente para ofrecer una experiencia de usuario instantánea y potente. Al utilizar las definiciones de tipos de bungie-api-ts, estrategias de caché con IndexedDB y algoritmos de poda para la optimización, es posible construir una plataforma unificada que defina el siguiente estándar en herramientas para la comunidad.

### **Referencias Clave Integradas**

* Documentación y definiciones de tipos: **bungie-api-ts**.8  
* Manejo del Manifiesto: **@d2api/manifest-web**.4  
* Repositorio Fuente DIM (Lógica de Inventario): **DestinyItemManager/DIM**.10  
* Repositorio Fuente D2AP (Algoritmos): **Mijago/D2ArmorPicker**.7  
* Documentación de Autenticación Bungie: **Bungie.net/en/Application**.7

#### **Obras citadas**

1. Manifest | BungieNetPlatform, fecha de acceso: diciembre 26, 2025, [https://destinydevs.github.io/BungieNetPlatform/docs/Manifest](https://destinydevs.github.io/BungieNetPlatform/docs/Manifest)  
2. Obtaining Destiny Definitions "The Manifest" · Bungie-net/api Wiki \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/Bungie-net/api/wiki/Obtaining-Destiny-Definitions-%22The-Manifest%22](https://github.com/Bungie-net/api/wiki/Obtaining-Destiny-Definitions-%22The-Manifest%22)  
3. bungie api help\! : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/18iklku/bungie\_api\_help/](https://www.reddit.com/r/DestinyTheGame/comments/18iklku/bungie_api_help/)  
4. @d2api/manifest-web \- npm, fecha de acceso: diciembre 26, 2025, [https://www.npmjs.com/package/@d2api/manifest-web](https://www.npmjs.com/package/@d2api/manifest-web)  
5. Bungie.Net API, fecha de acceso: diciembre 26, 2025, [https://bungie-net.github.io/](https://bungie-net.github.io/)  
6. Destiny – Allyn H, fecha de acceso: diciembre 26, 2025, [http://allynh.com/blog/tag/destiny/](http://allynh.com/blog/tag/destiny/)  
7. Mijago/D2ArmorPicker \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/Mijago/D2ArmorPicker](https://github.com/Mijago/D2ArmorPicker)  
8. README.md \- DestinyItemManager/bungie-api-ts \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/bungie-api-ts/blob/master/README.md](https://github.com/DestinyItemManager/bungie-api-ts/blob/master/README.md)  
9. DestinyItemManager/bungie-api-ts: TypeScript definitions for the Bungie.net API \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/bungie-api-ts](https://github.com/DestinyItemManager/bungie-api-ts)  
10. DestinyItemManager/DIM: Destiny Item Manager \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM](https://github.com/DestinyItemManager/DIM)  
11. Modules \- Job \#1281 \- DestinyItemManager/DIM \- RelativeCI, fecha de acceso: diciembre 26, 2025, [https://app.relative-ci.com/projects/MNCjiQtNRviIgMlL4XM6/jobs/1281-p0CSV6R1G2dCJFZ6TLoE/modules?bm=%7B%22entryId%22%3A%22.%2Fsrc%2Fapp%2Finventory%2FInventoryItem.m.scss%22%2C%22metric%22%3A%22totalSize%22%2C%22sortBy%22%3A%22runs%5B0%5D.delta%22%2C%22filters%22%3A%22changed-0\_md-0\_mst.fp-1\_mst.tp-1\_c.9-0\_c.14-0\_c.49-0\_c.50-0\_c.77-0\_c.82-0\_c.165-0\_c.179-1\_c.192-0\_c.199-0\_c.209-0\_c.216-0\_c.227-0\_c.234-0\_c.247-0\_c.302-0\_c.363-0\_c.400-0\_c.435-0\_c.481-0\_c.491-0\_c.495-0\_c.554-0\_c.566-0\_c.571-0\_c.575-0\_c.599-0\_c.666-0\_c.711-0\_c.744-0\_c.755-0\_c.792-0\_c.820-0\_c.863-0\_c.888-0\_c.898-0\_c.914-0\_c.938-0\_c.947-0\_c.988-0\_c.998-0\_mft.CSS-1\_mft.JS-1\_mft.OTHER-1%22%7D](https://app.relative-ci.com/projects/MNCjiQtNRviIgMlL4XM6/jobs/1281-p0CSV6R1G2dCJFZ6TLoE/modules?bm=%7B%22entryId%22:%22./src/app/inventory/InventoryItem.m.scss%22,%22metric%22:%22totalSize%22,%22sortBy%22:%22runs%5B0%5D.delta%22,%22filters%22:%22changed-0_md-0_mst.fp-1_mst.tp-1_c.9-0_c.14-0_c.49-0_c.50-0_c.77-0_c.82-0_c.165-0_c.179-1_c.192-0_c.199-0_c.209-0_c.216-0_c.227-0_c.234-0_c.247-0_c.302-0_c.363-0_c.400-0_c.435-0_c.481-0_c.491-0_c.495-0_c.554-0_c.566-0_c.571-0_c.575-0_c.599-0_c.666-0_c.711-0_c.744-0_c.755-0_c.792-0_c.820-0_c.863-0_c.888-0_c.898-0_c.914-0_c.938-0_c.947-0_c.988-0_c.998-0_mft.CSS-1_mft.JS-1_mft.OTHER-1%22%7D)  
12. Engrams in my inventory are shown under "POSTMASTER" · Issue \#5654 · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/issues/5654](https://github.com/DestinyItemManager/DIM/issues/5654)  
13. DIM/src/app/inventory/d1-stores.ts at master · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/blob/master/src/app/inventory/d1-stores.ts](https://github.com/DestinyItemManager/DIM/blob/master/src/app/inventory/d1-stores.ts)  
14. Calculation of Destiny 2 armor Base Stats incorrectly includes disabled Armor Mods · Issue \#6459 · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/issues/6459](https://github.com/DestinyItemManager/DIM/issues/6459)  
15. DestinyItemManager/dim-api: Destiny Item Manager API Service \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/dim-api](https://github.com/DestinyItemManager/dim-api)  
16. DIM/src/app/dim-api/dim-api-helper.ts at master · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/blob/master/src/app/dim-api/dim-api-helper.ts](https://github.com/DestinyItemManager/DIM/blob/master/src/app/dim-api/dim-api-helper.ts)  
17. How to handle React nested component circular dependency? (using es6 classes), fecha de acceso: diciembre 26, 2025, [https://stackoverflow.com/questions/35559631/how-to-handle-react-nested-component-circular-dependency-using-es6-classes](https://stackoverflow.com/questions/35559631/how-to-handle-react-nested-component-circular-dependency-using-es6-classes)  
18. Create react component dynamically \- javascript \- Stack Overflow, fecha de acceso: diciembre 26, 2025, [https://stackoverflow.com/questions/31234500/create-react-component-dynamically](https://stackoverflow.com/questions/31234500/create-react-component-dynamically)  
19. Inventory · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/wiki/Inventory/aaa52b23a77c353d1381059e66b8ae96741b39f1](https://github.com/DestinyItemManager/DIM/wiki/Inventory/aaa52b23a77c353d1381059e66b8ae96741b39f1)  
20. What's New \- DIM, fecha de acceso: diciembre 26, 2025, [https://app.destinyitemmanager.com/whats-new](https://app.destinyitemmanager.com/whats-new)  
21. Bungie.net is limiting how many requests DIM can make : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1kgicv1/bungienet\_is\_limiting\_how\_many\_requests\_dim\_can/](https://www.reddit.com/r/DestinyTheGame/comments/1kgicv1/bungienet_is_limiting_how_many_requests_dim_can/)  
22. I made a guide for DIM / Inventory Management / the Loadout Optimizer \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/p75x5b/i\_made\_a\_guide\_for\_dim\_inventory\_management\_the/](https://www.reddit.com/r/DestinyTheGame/comments/p75x5b/i_made_a_guide_for_dim_inventory_management_the/)  
23. Item Search · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/wiki/Item-Search](https://github.com/DestinyItemManager/DIM/wiki/Item-Search)  
24. D2ArmorPicker/package.json at master \- Mijago \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/Mijago/D2ArmorPicker/blob/master/package.json](https://github.com/Mijago/D2ArmorPicker/blob/master/package.json)  
25. harleygilpin/soc-audit-11k · Datasets at Hugging Face, fecha de acceso: diciembre 26, 2025, [https://huggingface.co/datasets/harleygilpin/soc-audit-11k/viewer](https://huggingface.co/datasets/harleygilpin/soc-audit-11k/viewer)  
26. D2ArmorPicker and Artifice Armor Build-Crafting : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/11svyju/d2armorpicker\_and\_artifice\_armor\_buildcrafting/](https://www.reddit.com/r/DestinyTheGame/comments/11svyju/d2armorpicker_and_artifice_armor_buildcrafting/)  
27. In case no one has posted this before, here's a great website to make your perfect stat combos : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/pugpad/in\_case\_no\_one\_has\_posted\_this\_before\_heres\_a/](https://www.reddit.com/r/DestinyTheGame/comments/pugpad/in_case_no_one_has_posted_this_before_heres_a/)  
28. D2ArmorPicker V2.4.0 brings back the features you missed : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/13rngz5/d2armorpicker\_v240\_brings\_back\_the\_features\_you/](https://www.reddit.com/r/DestinyTheGame/comments/13rngz5/d2armorpicker_v240_brings_back_the_features_you/)  
29. Easy Armor Guide to Flat Stat Builds (NO WASTED STATS) / Destiny 2 \- YouTube, fecha de acceso: diciembre 26, 2025, [https://www.youtube.com/watch?v=Dr2uA0zEnKk](https://www.youtube.com/watch?v=Dr2uA0zEnKk)  
30. Organizer · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/wiki/Organizer](https://github.com/DestinyItemManager/DIM/wiki/Organizer)  
31. Home · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/wiki](https://github.com/DestinyItemManager/DIM/wiki)