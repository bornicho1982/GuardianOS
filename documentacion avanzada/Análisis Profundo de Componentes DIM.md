# **Análisis Arquitectónico Integral de Destiny Item Manager (DIM): Una Disección Profunda del Código Fuente, Gestión de Estado y Estructuras de Datos**

## **1\. Introducción y Alcance de la Investigación**

Este informe técnico presenta una investigación exhaustiva sobre la arquitectura de software, la composición del código fuente y los mecanismos de ingeniería de datos detrás de **Destiny Item Manager (DIM)**. DIM es la herramienta de gestión de inventario de terceros más prominente para la franquicia de videojuegos *Destiny*, operando como una Aplicación Web Progresiva (PWA) de código abierto.1 A diferencia de las aplicaciones web convencionales que dependen en gran medida del renderizado del lado del servidor, DIM funciona como un sistema de gestión de bases de datos del lado del cliente, ingiriendo, normalizando y consultando conjuntos de datos masivos en tiempo real dentro del navegador del usuario.

El propósito de este análisis es desglosar "absolutamente todo" sobre la composición de su inventario, la estructura de sus archivos JavaScript (TypeScript) y CSS, la jerarquía de componentes y subcomponentes, y el uso de bases de datos propias y externas. Se prestará especial atención a la reciente migración tecnológica hacia **StatelyDB** para la sincronización en la nube 3, la implementación de persistencia local mediante **IndexedDB** 4, y la adaptación de la lógica de negocio para soportar los complejos cambios mecánicos introducidos en la actualización "Edge of Fate" (Armor 3.0).6

La investigación revela que DIM no es simplemente una interfaz visual sobre la API de Bungie, sino un motor de procesamiento de datos complejo que utiliza **Redux Toolkit** para la gestión de estado transaccional, **Web Workers** para cálculos algorítmicos intensivos (como la optimización de equipamiento), y una arquitectura de estilos modular basada en **CSS Modules** y **SCSS** para garantizar un rendimiento de renderizado de 60 cuadros por segundo.8

## ---

**2\. Arquitectura del Sistema de Archivos y Organización del Código Fuente**

El repositorio de DIM es un monorepositorio masivo basado en TypeScript. La estructura del proyecto no está organizada por tipo de archivo (no hay carpetas genéricas de "controladores" o "vistas"), sino por **dominio funcional**. Esta arquitectura, conocida como "feature-based folder structure", permite que cada subsistema del aplicativo (Inventario, Loadouts, Cuentas) sea modular y mantenible. A continuación, se detalla la composición profunda de la carpeta raíz src/app, que constituye el sistema nervioso de la aplicación.10

### **2.1 El Núcleo de la Aplicación: src/app**

La carpeta src/app contiene la lógica de negocio y la interfaz de usuario. Cada subcarpeta representa un módulo funcional completo con sus propios componentes, reductores de estado (Redux slices), estilos y lógica de API.

#### **2.1.1 accounts/: Gestión de Identidad y Plataformas**

Este directorio gestiona la autenticación con Bungie.net mediante el protocolo OAuth 2.0.

* **Archivos Clave:**  
  * actions.ts: Define las acciones de Redux para iniciar sesión, cerrar sesión y manejar errores de autenticación.10  
  * platforms.ts: Contiene la lógica para distinguir entre las diferentes plataformas de juego (Xbox, PlayStation, Steam, Epic). Dado que un usuario puede tener cuentas en múltiples plataformas vinculadas a un solo perfil de Bungie (Cross-Save), este módulo es crítico para determinar qué conjunto de personajes cargar.10  
  * selectors.ts: Funciones para extraer la cuenta actual del árbol de estado global de Redux.

#### **2.1.2 bungie-api/: La Capa de Abstracción de Red**

DIM no utiliza un SDK generado automáticamente, sino una colección curada de funciones que envuelven los endpoints REST de Bungie. Esta carpeta aísla la aplicación de los detalles de implementación de la red.

* **Componentes Internos:**  
  * destiny2-api.ts: Contiene funciones tipadas para endpoints críticos como GetProfile, TransferItem, EquipItem y PullFromPostmaster. Estas funciones devuelven promesas tipadas con interfaces generadas desde el manifiesto de Bungie (bungie-api-ts).11  
  * authenticated-fetch.ts: Un middleware personalizado para fetch. Intercepta cada solicitud saliente para inyectar el encabezado Authorization: Bearer {token}. Además, maneja la lógica de reintento: si recibe un error 401 (No autorizado), intenta automáticamente usar el *refresh token* para obtener un nuevo token de acceso sin cerrar la sesión del usuario.12  
  * rate-limiter.ts: Un sistema de cola sofisticado. La API de Bungie impone límites estrictos de tasa. Este archivo implementa un "leaky bucket" o algoritmo similar para asegurar que DIM no exceda las solicitudes permitidas, poniendo en cola las operaciones de movimiento de ítems si es necesario para evitar baneos temporales.

#### **2.1.3 destiny2/ vs destiny1/: Segregación de Versiones**

DIM mantiene soporte para el juego original *Destiny 1*. El código específico para cada juego está separado.

* **destiny2/**: Contiene d2-definitions.ts, que maneja la carga y el almacenamiento en caché de las definiciones del Manifiesto de D2 (la base de datos estática de todos los ítems posibles). También incluye lógica específica de D2 como los "Milestones" (hitos semanales) y la progresión de temporada.10

#### **2.1.4 dim-api/: Sincronización en la Nube**

Este módulo es el puente entre el cliente (navegador) y el servicio backend propio de DIM (api.destinyitemmanager.com).

* **Función:** Permite guardar datos que la API de Bungie no soporta: etiquetas personalizadas ("Junk", "Favorite", "Infuse"), notas de usuario, loadouts personalizados y configuraciones de la aplicación.12  
* **Arquitectura de Datos:** Utiliza un modelo de sincronización basado en transacciones. El cliente acumula una lista de cambios (deltas) y los envía en lote al servidor.  
* **dim-api-types**: Un paquete separado (mencionado en package.json 13) que comparte las definiciones de tipo TypeScript entre el cliente y el servidor, garantizando que ambos "hablen" el mismo idioma de datos.12

#### **2.1.5 inventory/: El Corazón del Sistema**

Este es el directorio más complejo y volumétrico. Contiene toda la lógica para modelar, almacenar y manipular el inventario del usuario.

* **store/**: Contiene la lógica de negocio pura (no UI).  
  * d2-stores.ts: El archivo más crítico para la carga de datos. Orquesta la llamada a GetProfile, descarga el Manifiesto, y fusiona ambos conjuntos de datos para crear el objeto D2Store que representa a los personajes y el depósito.10  
  * d2-item-factory.ts: Una fábrica de software masiva que transforma los datos crudos y crípticos de Bungie (hashes y enteros) en objetos DimItem ricos y utilizables por la UI. Aquí es donde se calcula el poder real de un ítem, se aplican los mods y se determina si un ítem es "Masterwork".15  
  * inventory-reducer.ts: El reductor de Redux que maneja el estado del inventario. Procesa acciones como updateCharacters (actualización masiva) o itemMoved (actualización optimista cuando el usuario arrastra un ítem).8  
* **Inventory.tsx**: El componente React principal que renderiza la cuadrícula de inventario.  
* **InventoryItem.tsx**: El componente visual que representa un solo ítem (el "tile").  
* **DraggableInventoryItem.tsx**: Un componente envoltorio (High-Order Component) que dota al ítem de capacidades de "Drag and Drop" (arrastrar y soltar).18

#### **2.1.6 loadout/ y loadout-builder/: Motores de Optimización**

* **loadout/**: Gestiona la creación, edición y aplicación de equipamientos guardados. Contiene la lógica para verificar si un loadout es aplicable (por ejemplo, si el usuario tiene espacio en el inventario).  
* **loadout-builder/**: Contiene el "Loadout Optimizer" (anteriormente Loadout Builder). Este no es solo una UI, sino un **motor de resolución de restricciones**.  
  * Utiliza algoritmos matemáticos (Branch and Bound) para encontrar la combinación óptima de armadura que cumpla con los requisitos de estadísticas del usuario (e.g., Tier 10 Resistencia, Tier 8 Recuperación).19  
  * Delega el procesamiento pesado a **Web Workers** (\*.worker.ts) para evitar congelar la interfaz principal.20

#### **2.1.7 search/: El Motor de Consultas**

DIM posee un lenguaje de consultas propio (e.g., is:weapon and (perk:outlaw or stat:range\>50)).

* **search-filter.ts**: Contiene el lexer y parser que descomponen estas cadenas de texto en un árbol de sintaxis abstracta (AST) y luego en funciones de filtrado ejecutables.  
* **search-config.ts**: Define todas las palabras clave disponibles y su lógica asociada.

#### **2.1.8 shell/: La Estructura de la UI**

Maneja el "esqueleto" de la aplicación.

* Header, Footer y Menú de navegación.  
* **Sistema de Notificaciones**: Un bus de eventos global para mostrar tostadas (toasts) de éxito o error (e.g., "Ítem transferido correctamente").  
* **Loading Trackers**: Indicadores de carga globales que escuchan las promesas de red activas.

## ---

**3\. Composición de JavaScript (TypeScript) y Gestión de Estado**

La columna vertebral lógica de DIM está construida sobre **TypeScript**, **React** y **Redux Toolkit**. Esta combinación permite un tipado estricto, una interfaz reactiva y un manejo predecible del estado de la aplicación.

### **3.1 Gestión de Estado con Redux Toolkit (RTK)**

DIM no utiliza el estado local de React para datos críticos; utiliza un "Single Source of Truth" (Fuente Única de Verdad) mediante Redux. El estado global es un objeto gigante que contiene todo: inventario, definiciones del manifiesto, configuración de usuario y estado de autenticación.

#### **3.1.1 Slices y Reducers**

El estado se divide en "slices" (rebanadas) gestionadas por RTK 22:

* **inventory**: Almacena la lista de stores (personajes y depósito). Cada store contiene un array de items.  
* **accounts**: Almacena el token OAuth y la plataforma activa.  
* **manifest**: Almacena las definiciones estáticas cargadas desde IndexedDB.  
* **loadouts**: Almacena los equipamientos guardados por el usuario.

#### **3.1.2 Acciones Asíncronas (Thunks)**

DIM hace un uso extensivo de createAsyncThunk de RTK para manejar operaciones asíncronas complejas.

* Ejemplo: loadStores (en d2-stores.ts):  
  Esta es la "acción maestra". Cuando se despacha:  
  1. Verifica si el token de autenticación es válido.  
  2. Verifica si el Manifiesto está cargado; si no, inicia su carga desde IndexedDB.  
  3. Realiza la llamada a la API de Bungie (getProfile).  
  4. Procesa los datos con d2-item-factory para crear instancias de DimItem.  
  5. Despacha la acción updateCharacters para reemplazar el estado del inventario en Redux con los nuevos datos.10

#### **3.1.3 Middleware Personalizado**

DIM implementa middleware en la cadena de Redux para propósitos específicos:

* **Middleware de Errores**: Intercepta errores de la API de Bungie (como códigos de error 500 o códigos de mantenimiento) y despacha acciones globales de notificación para alertar al usuario.  
* **Middleware de Analíticas**: Observa ciertas acciones (como "Loadout Applied") y envía telemetría anónima para análisis de uso del producto.

### **3.2 Arquitectura de Componentes React**

DIM ha migrado de una arquitectura antigua basada en clases (e incluso AngularJS en sus inicios) a una arquitectura moderna basada en **Functional Components** y **Hooks**.

* **Custom Hooks**: DIM abstrae lógica compleja en hooks reutilizables.  
  * useLoadStores(): Un hook que encapsula la suscripción a las actualizaciones del inventario.  
  * useSubscription(): Para manejar suscripciones a observables de RxJS (usados en algunas partes heredadas para flujos de eventos).  
* **Patrón Contenedor/Presentación**:  
  * Los componentes de alto nivel (como Inventory) están "conectados" a Redux (useSelector, useDispatch). Obtienen los datos y los pasan hacia abajo.  
  * Los componentes de bajo nivel (como InventoryItem) son "puros". Solo reciben props y renderizan la interfaz. Esto maximiza el rendimiento al permitir que React optimice el renderizado mediante React.memo.16

## ---

**4\. Composición de CSS y Arquitectura de Estilos**

DIM enfrenta un desafío único: debe mostrar cientos de íconos de alta fidelidad, barras de estadísticas y elementos interactivos simultáneamente, manteniendo un rendimiento fluido. Para lograr esto, utiliza una arquitectura de estilos altamente modular y precompilada.

### **4.1 SCSS y CSS Modules**

DIM utiliza **SASS (SCSS)** como preprocesador, pero lo implementa a través de la metodología de **CSS Modules**.24

* **Archivos .m.scss**: Los archivos de estilo se nombran con la extensión .m.scss (e.g., InventoryItem.m.scss).  
* **Aislamiento de Ámbito (Scope Isolation)**: Cuando el código se compila (mediante Webpack/Rspack), las clases CSS definidas en estos archivos se transforman en cadenas hash únicas (e.g., .item se convierte en .InventoryItem\_item\_\_x9z2).  
  * *Ventaja*: Esto elimina por completo los conflictos de nombres CSS. Un desarrollador puede usar la clase .container en diez componentes diferentes sin que los estilos se "filtren" o sobrescriban entre sí.  
* **Uso en Código**:  
  TypeScript  
  import styles from './InventoryItem.m.scss';  
  //...  
  \<div className\={styles.item}\>... \</div\>

  El objeto styles es un mapa generado durante la compilación que vincula el nombre original con el nombre hash.26

### **4.2 Variables Globales y Tematización**

A pesar del aislamiento de módulos, DIM necesita coherencia visual. Esto se logra mediante:

* **variables.scss**: Un archivo global que define "tokens de diseño": colores ($solar-orange, $void-purple), tamaños de fuente, puntos de ruptura (breakpoints) para diseño responsivo y espaciado.  
* **Propiedades Personalizadas de CSS (Variables CSS)**: Para la tematización (Modo Claro, Modo Oscuro, Modo Negro OLED), DIM utiliza variables nativas de CSS (e.g., \--theme-bg, \--theme-text).  
  * *Mecanismo*: Al cambiar el tema en la configuración, simplemente se cambia una clase en el elemento \<body\> o raíz, lo que reasigna los valores de estas variables CSS globalmente sin necesidad de re-renderizar el árbol de componentes de React.27

## ---

**5\. Ingeniería de Datos: Bases de Datos y Persistencia**

DIM es una aplicación "local-first" en su filosofía operativa. Aunque los datos "viven" en Bungie, DIM necesita una copia local robusta para funcionar con rapidez.

### **5.1 IndexedDB: La Base de Datos del Navegador**

El almacenamiento local estándar (localStorage) está limitado a unos 5-10 MB, lo cual es insuficiente para DIM. El Manifiesto de *Destiny 2* (la base de datos de definiciones) puede superar los 60-100 MB de datos JSON descomprimidos.

* **Implementación**: DIM utiliza **IndexedDB**, una base de datos NoSQL transaccional integrada en el navegador.4  
* **Bibliotecas**: Se utiliza idb-keyval para operaciones simples de clave-valor (como guardar la configuración del usuario) y envoltorios más directos o librerías ligeras para el manejo del manifiesto. Aunque algunos snippets mencionan dexie en el contexto de D2ArmorPicker 28, DIM tiende a minimizar dependencias externas pesadas, prefiriendo implementaciones optimizadas para su caso de uso específico.  
* **Estructura de Almacenamiento**:  
  * **Store manifest**: Almacena las tablas del manifiesto de Bungie. Cuando Bungie actualiza el juego, DIM detecta un cambio en la versión del manifiesto, descarga el nuevo archivo SQLite/JSON, lo parsea y actualiza estos registros en IndexedDB. Esto permite que la aplicación cargue las definiciones de ítems instantáneamente en visitas posteriores sin volver a descargar megabytes de datos.  
  * **Store key-value**: Almacena preferencias de usuario locales que no necesitan sincronizarse o que actúan como caché (e.g., el último perfil cargado).

### **5.2 DIM Sync y la Migración a StatelyDB**

Para los datos que deben persistir entre dispositivos (etiquetas, notas, loadouts), DIM utiliza su propia API.

* **Infraestructura Anterior**: Históricamente basada en **PostgreSQL**, una base de datos relacional SQL estándar.  
* **Nueva Infraestructura (StatelyDB)**: Investigaciones recientes 3 indican una migración hacia **StatelyDB**.  
  * *Por qué StatelyDB?*: StatelyDB ofrece un modelo de datos basado en **máquinas de estados** y esquemas flexibles. Esto es ideal para la sincronización de datos de usuario donde los conflictos pueden ocurrir (e.g., etiquetar un ítem en el móvil mientras se edita un loadout en el PC). StatelyDB permite manejar "deltas" (cambios incrementales) de manera más nativa que una tabla SQL rígida, facilitando una sincronización más rápida y resiliente.  
* **Protocolo**: El cliente (dim-api/) envía lotes de actualizaciones a api.destinyitemmanager.com. El servidor procesa estas transacciones y devuelve el estado actualizado consolidado.

## ---

**6\. Componentes y Subcomponentes del Inventario**

El inventario es una jerarquía compleja de componentes React diseñados para la virtualización y el rendimiento.

1. **InventoryGrid (Componente Padre)**: El contenedor principal. Calcula cuántas columnas mostrar basándose en el ancho de la ventana.  
2. **StoreBucket (Columna)**: Representa a una entidad (Hechicero, Titán, Cazador, Depósito).  
3. **InventoryBucket (Grupo)**: Representa una categoría dentro de una entidad (Armas Cinéticas, Cascos, Consumibles).  
4. **DraggableInventoryItem (Interactivo)**: Envuelve el ítem visual. Utiliza librerías como use-gesture o la API nativa de HTML5 Drag and Drop para gestionar el movimiento.  
   * *Lógica*: Al iniciar el arrastre, captura el ID del ítem. Al soltar ("drop"), detecta el contenedor destino (otro personaje o el depósito) y dispara la acción moveItem.  
5. **InventoryItem (Visual)**: El "ladrillo" fundamental. Renderiza:  
   * **Icono**: Imagen de fondo (obtenida dinámicamente de bungie.net).  
   * **Bordes**: Coloreados por rareza (Excepcional, Leyenda).  
   * **Overlays**: Icono de temporada, nivel de poder, barra de progreso (para contratos), iconos de bloqueo/etiqueta.  
   * **Sockets**: Pequeños indicadores de qué mods o perks están activos.

## ---

**7\. Adaptación a "Armor 3.0" (Edge of Fate) y Lógica de ItemFactory**

La actualización "Armor 3.0" introducida en la expansión "Edge of Fate" representa un cambio paradigmático en la estructura de datos que DIM debe manejar.6

### **7.1 Nuevas Estructuras de Datos**

El sistema cambia de las estadísticas tradicionales (Movilidad, Resistencia, Recuperación) a un nuevo conjunto: **Armas, Salud, Clase, Granada, Súper, Cuerpo a Cuerpo**.6 Además, la escala de estadísticas cambia de 0-100 a **0-200**.30

* **Impacto en d2-item-factory.ts**: Este archivo debe ser reescrito para mapear los nuevos statHash provenientes de la API de Bungie. Ya no puede asumir un límite de 100\. Debe leer las definiciones de DestinyStatGroupDefinition para aplicar las nuevas curvas de interpolación visual.

### **7.2 Arquetipos de Armadura y Mods de Ajuste (Tuning)**

Las nuevas armaduras tienen "Arquetipos" (e.g., Gunner, Brawler) que definen sus estadísticas primarias y secundarias.31 Además, las armaduras de "Tier 5" tienen un slot de "Tuning" que permite intercambiar estadísticas (+5 a una, \-5 a otra).7

* **Implementación en Código**:  
  * El ItemFactory debe inspeccionar los sockets (enchufes) del ítem. Si detecta un mod de "Tuning", debe aplicar matemáticamente ese modificador al array de stats del objeto DimItem *antes* de que llegue a la UI.  
  * Esto es crítico para el **Loadout Optimizer**. Si el optimizador leyera las estadísticas base sin el mod de tuning, calcularía mal los tiers resultantes. El código debe simular el efecto del mod en memoria.

## ---

**8\. Algoritmos de Optimización: El Optimizador de Loadouts**

El "Loadout Optimizer" es una de las joyas de ingeniería de DIM. Su objetivo es resolver un problema matemático de **Satisfacción de Restricciones (CSP)** o una variante del problema de la **Mochila Multidimensional**.

### **8.1 El Problema Matemático**

Dado un inventario de 500 piezas de armadura, encontrar una combinación de 5 piezas (Casco, Guantes, Pecho, Piernas, Clase) tal que:

1. La suma de estadísticas cumpla con los requisitos del usuario (e.g., Total \> 300, Salud \> 100).  
2. Se respeten las restricciones de exóticos (solo uno permitido).  
3. Se respeten los costos de energía de los mods.

### **8.2 El Algoritmo: Branch and Bound (Ramificación y Poda)**

Un enfoque de fuerza bruta sería imposible (billones de combinaciones). DIM utiliza **Branch and Bound**.19

1. **Ramificación**: El algoritmo fija una pieza (e.g., Casco A) y luego explora combinaciones con las piezas restantes.  
2. **Poda (Bounding)**: En cada paso, calcula el *máximo potencial restante*. Si (Estadísticas Actuales \+ Máximo Posible de las piezas restantes) \< Objetivo, descarta (poda) esa rama entera inmediatamente. Esto reduce el espacio de búsqueda de billones a miles en milisegundos.

### **8.3 Web Workers**

Para evitar que este cálculo intensivo congele la interfaz de usuario (el hilo principal de JavaScript), DIM lo ejecuta en un **Web Worker** (loadout-optimizer.worker.ts).20

* El hilo principal serializa los ítems y los envía al Worker.  
* El Worker ejecuta el algoritmo y devuelve los resultados ("Sets válidos") en flujo (streaming) al hilo principal para que se rendericen progresivamente.

## ---

**9\. Conclusión**

La arquitectura de Destiny Item Manager es un ejemplo sobresaliente de ingeniería de software moderna en el lado del cliente. Al combinar **IndexedDB** para el almacenamiento masivo de datos estáticos, **Redux Toolkit** para un control de estado transaccional preciso, y **Web Workers** para el procesamiento algorítmico paralelo, DIM logra ofrecer una experiencia de usuario que rivaliza con aplicaciones nativas de escritorio. Su estructura modular de archivos y su sistema de estilos aislado (CSS Modules) aseguran que el proyecto sea escalable y mantenible, capaz de adaptarse a cambios radicales en el juego subyacente, como la transición a Armor 3.0, sin requerir una reescritura total del núcleo de la aplicación.

#### **Obras citadas**

1. Home · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/wiki](https://github.com/DestinyItemManager/DIM/wiki)  
2. About DIM \- Destiny Item Manager, fecha de acceso: diciembre 26, 2025, [https://beta.destinyitemmanager.com/about](https://beta.destinyitemmanager.com/about)  
3. Migrating Destiny Item Manager to StatelyDB \- Stately Cloud, fecha de acceso: diciembre 26, 2025, [https://stately.cloud/blog/migrating-destiny-item-manager-to-statelydb](https://stately.cloud/blog/migrating-destiny-item-manager-to-statelydb)  
4. createIndexedDbPersister \- TinyBase, fecha de acceso: diciembre 26, 2025, [https://tinybase.org/api/persister-indexed-db/functions/creation/createindexeddbpersister/](https://tinybase.org/api/persister-indexed-db/functions/creation/createindexeddbpersister/)  
5. Best Practices for Persisting Application State with IndexedDB | Articles \- web.dev, fecha de acceso: diciembre 26, 2025, [https://web.dev/articles/indexeddb-best-practices-app-state](https://web.dev/articles/indexeddb-best-practices-app-state)  
6. Destiny 2 Armor 3.0 Guide – Tier System, Stats, Sets & Bonuses \- Skycoach, fecha de acceso: diciembre 26, 2025, [https://skycoach.gg/blog/destiny/articles/armor-3-0-guide](https://skycoach.gg/blog/destiny/articles/armor-3-0-guide)  
7. Destiny 2 Armor 3.0 Explained \- Complete Edge of Fate Guide \- Boosting Ground, fecha de acceso: diciembre 26, 2025, [https://boosting-ground.com/Destiny2/guides/pve-guides/armor-3-0-complete-guide](https://boosting-ground.com/Destiny2/guides/pve-guides/armor-3-0-complete-guide)  
8. DestinyItemManager/DIM: Destiny Item Manager \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM](https://github.com/DestinyItemManager/DIM)  
9. DIM/.stylelintrc at master · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/blob/master/.stylelintrc](https://github.com/DestinyItemManager/DIM/blob/master/.stylelintrc)  
10. DIM/src/app/inventory/d1-stores.ts at master · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/blob/master/src/app/inventory/d1-stores.ts](https://github.com/DestinyItemManager/DIM/blob/master/src/app/inventory/d1-stores.ts)  
11. Destiny Item Manager \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/destinyitemmanager](https://github.com/destinyitemmanager)  
12. DestinyItemManager/dim-api: Destiny Item Manager API Service \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/dim-api](https://github.com/DestinyItemManager/dim-api)  
13. package.json \- DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/blob/master/package.json](https://github.com/DestinyItemManager/DIM/blob/master/package.json)  
14. Cannot transfer or equip items in DIM · Issue \#10320 · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/issues/10320](https://github.com/DestinyItemManager/DIM/issues/10320)  
15. Change weapon pop-ups to indicate if a weapon is 'deepsight harmonizable' · Issue \#9810 · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/issues/9810](https://github.com/DestinyItemManager/DIM/issues/9810)  
16. Something went wrong. S in not iterable. · Issue \#8557 · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/issues/8557](https://github.com/DestinyItemManager/DIM/issues/8557)  
17. I made a guide for DIM / Inventory Management / the Loadout Optimizer \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/p75x5b/i\_made\_a\_guide\_for\_dim\_inventory\_management\_the/](https://www.reddit.com/r/DestinyTheGame/comments/p75x5b/i_made_a_guide_for_dim_inventory_management_the/)  
18. Inventory · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/wiki/Inventory](https://github.com/DestinyItemManager/DIM/wiki/Inventory)  
19. How to use DIM and D2Armorpicker's Loadout Optimizers | Destiny 2 Lightfall Prep, fecha de acceso: diciembre 26, 2025, [https://www.youtube.com/watch?v=jOi4wwuQPe8](https://www.youtube.com/watch?v=jOi4wwuQPe8)  
20. Mijago/D2ArmorPicker \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/Mijago/D2ArmorPicker](https://github.com/Mijago/D2ArmorPicker)  
21. D2ArmorPicker V2.4.0 brings back the features you missed : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/13rngz5/d2armorpicker\_v240\_brings\_back\_the\_features\_you/](https://www.reddit.com/r/DestinyTheGame/comments/13rngz5/d2armorpicker_v240_brings_back_the_features_you/)  
22. Usage With TypeScript \- Redux Toolkit, fecha de acceso: diciembre 26, 2025, [https://redux-toolkit.js.org/usage/usage-with-typescript](https://redux-toolkit.js.org/usage/usage-with-typescript)  
23. Usage Guide \- Redux Toolkit, fecha de acceso: diciembre 26, 2025, [https://redux-toolkit.js.org/usage/usage-guide](https://redux-toolkit.js.org/usage/usage-guide)  
24. Katie Fenn: Writing Modular Stylesheets with CSS Modules \- YouTube, fecha de acceso: diciembre 26, 2025, [https://www.youtube.com/watch?v=LKY-BxF31aw](https://www.youtube.com/watch?v=LKY-BxF31aw)  
25. Start doing THIS to improve your CSS Architecture \- DEV Community, fecha de acceso: diciembre 26, 2025, [https://dev.to/juanoa/start-doing-this-to-improve-your-css-architecture-n3n](https://dev.to/juanoa/start-doing-this-to-improve-your-css-architecture-n3n)  
26. Writing maintainable styles and components with CSS Modules \- Medium, fecha de acceso: diciembre 26, 2025, [https://medium.com/@skovy/writing-maintainable-styles-and-components-with-css-modules-308a9216a6c2](https://medium.com/@skovy/writing-maintainable-styles-and-components-with-css-modules-308a9216a6c2)  
27. What do you prefer Styled Components or CSS modules? : r/webdev \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/webdev/comments/sxjqoj/what\_do\_you\_prefer\_styled\_components\_or\_css/](https://www.reddit.com/r/webdev/comments/sxjqoj/what_do_you_prefer_styled_components_or_css/)  
28. Dexie.js \- Offline-First Database with Cloud Sync, Collaboration & Real-Time Updates, fecha de acceso: diciembre 26, 2025, [https://dexie.org/](https://dexie.org/)  
29. Armor Archetypes in Armor 3.0 : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1l2op8y/armor\_archetypes\_in\_armor\_30/](https://www.reddit.com/r/DestinyTheGame/comments/1l2op8y/armor_archetypes_in_armor_30/)  
30. Destiny 2: Armor 3.0 & Buildcrafting \- Shattered Vault, fecha de acceso: diciembre 26, 2025, [https://shatteredvault.com/kb/armour-buildcrafting/](https://shatteredvault.com/kb/armour-buildcrafting/)  
31. Destiny 2 Armour 3.0: Stat changes and archetypes in Edge of Fate \- PC Gamer, fecha de acceso: diciembre 26, 2025, [https://www.pcgamer.com/games/fps/destiny-2-armour-3-0-stats-archetypes/](https://www.pcgamer.com/games/fps/destiny-2-armour-3-0-stats-archetypes/)  
32. The Definitive Edge of Fate Armor Stat Planner : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1l4ogzu/the\_definitive\_edge\_of\_fate\_armor\_stat\_planner/](https://www.reddit.com/r/DestinyTheGame/comments/1l4ogzu/the_definitive_edge_of_fate_armor_stat_planner/)  
33. Branch and Bound Algorithm \- GeeksforGeeks, fecha de acceso: diciembre 26, 2025, [https://www.geeksforgeeks.org/dsa/branch-and-bound-algorithm/](https://www.geeksforgeeks.org/dsa/branch-and-bound-algorithm/)