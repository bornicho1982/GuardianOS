# **Arquitectura Sistémica y Análisis de Componentes del Módulo de Inventario en Destiny Item Manager (DIM)**

## **Resumen Ejecutivo y Alcance Arquitectónico**

La ingeniería detrás de Destiny Item Manager (DIM), específicamente su módulo de inventario, representa un caso de estudio paradigmático en la construcción de Aplicaciones Web Progresivas (PWA) de alta complejidad utilizando el ecosistema de React y TypeScript. Este informe técnico despliega una investigación exhaustiva sobre los mecanismos de extracción de datos, normalización de estado, jerarquía de componentes visuales y estrategias de estilizado modular que permiten a DIM gestionar miles de objetos de juego en tiempo real.1

El núcleo de la aplicación se basa en una arquitectura unidireccional de flujo de datos, orquestada por Redux, donde la inmutabilidad y la estricta tipificación de TypeScript (.ts) aseguran la integridad de los datos antes de que siquiera toquen la capa de presentación (.tsx). La interfaz de usuario no es meramente una colección de vistas estáticas, sino un sistema reactivo donde componentes de alto nivel como Inventory.tsx actúan como controladores de tráfico, delegando la responsabilidad de renderizado a estructuras anidadas como Stores.tsx y StoreBucket.tsx, las cuales encapsulan lógica de negocio específica para cada tipo de ítem y ubicación de almacenamiento.3

Este documento desglosa la anatomía del sistema archivo por archivo, extensión por extensión, explicando cómo los módulos de lógica pura (.ts) invocan servicios de la API de Bungie, transforman respuestas JSON masivas en modelos de dominio enriquecidos (DimItem), y cómo estos modelos son consumidos por componentes de React (.tsx) estilizados mediante módulos de Sass (.m.scss) para garantizar un encapsulamiento visual robusto. A través de este análisis, se revelará cómo DIM "engloba" la complejidad del juego en una interfaz fluida y funcional.

## ---

**1\. La Capa de Datos: Extracción, Normalización y Lógica de Negocio (.ts)**

Para comprender cómo DIM "saca todo" y lo representa, es imperativo comenzar no por la interfaz visual, sino por los cimientos invisibles que residen en los archivos TypeScript (.ts). Estos archivos constituyen el cerebro de la aplicación, manejando la comunicación asíncrona y la integridad de los datos.

### **1.1 El Motor de Extracción: d2-stores.ts y la API de Bungie**

El proceso de poblar la página de inventario comienza mucho antes de que se renderice cualquier píxel. El archivo src/app/inventory/d2-stores.ts es el responsable de iniciar la secuencia de carga de datos.3 Este módulo exporta funciones tipo "thunk" (acciones asíncronas de Redux) como loadStores, que actúan como el punto de entrada para la recuperación de datos.

Cuando la aplicación inicia o el usuario solicita una actualización, loadStores orquesta una serie de llamadas paralelas críticas. En primer lugar, verifica la existencia de una sesión válida y obtiene las definiciones del manifiesto de Destiny (defs). El manifiesto es una base de datos SQLite masiva que reside en el cliente, conteniendo información estática sobre cada ítem posible en el juego (nombres, descripciones, rutas de iconos). Sin este manifiesto, la aplicación solo tendría identificadores numéricos (hashes) sin contexto semántico.3

Simultáneamente, el sistema invoca la función getProfile de la librería bungie-api-ts. Esta solicitud es quirúrgica; no pide todo el perfil del usuario, sino que especifica una lista de componentes necesarios mediante enumeraciones de DestinyComponentType. Para construir la vista de inventario completa, DIM solicita:

1. ProfileInventories: Ítems en el depósito y monedas globales.  
2. CharacterInventories: Ítems en los "buckets" de cada personaje (armas, armaduras, consumibles).  
3. CharacterEquipment: Ítems equipados actualmente.  
4. ItemComponents: Datos granulares de cada instancia de ítem, incluyendo estadísticas (ItemStats), ventajas (ItemSockets), y objetivos (ItemObjectives).5

Esta respuesta cruda de la API es un objeto JSON profundamente anidado y normalizado, donde los ítems están separados de sus dueños y de sus estadísticas. Aquí es donde entra en juego la lógica de transformación de DIM.

### **1.2 La Fábrica de Objetos: d2-item-factory.ts**

Una vez que los datos crudos llegan al cliente, deben ser transformados en algo que la interfaz de usuario pueda consumir fácilmente. El archivo src/app/inventory/store/d2-item-factory.ts es el encargado de esta alquimia. Su función principal, processItems, itera sobre cada ítem crudo devuelto por la API y lo "infla" utilizando las definiciones del manifiesto y reglas de negocio personalizadas.

El proceso de creación de un DimItem (la interfaz TypeScript que representa un ítem en DIM) es exhaustivo. El código toma el itemHash de la respuesta de la API y busca su definición estática. Luego, combina esta definición con los datos de la instancia específica (el ítem "real" que posee el jugador). Por ejemplo, mientras que la definición estática dice que el arma "The Palindrome" *puede* tener ciertas estadísticas, la instancia específica dice qué cañón, cargador y obra maestra tiene ese ítem en particular, lo que altera sus estadísticas finales.

d2-item-factory.ts también es responsable de calcular propiedades derivadas que no existen en la API de Bungie. Por ejemplo, calcula el porcentaje de calidad de las estadísticas base en comparación con el máximo posible para ese tipo de armadura, una métrica crucial para los jugadores que buscan optimizar sus equipamientos. También verifica si el ítem está en una "Wishlist" (lista de deseos) comparando sus ventajas (perks) con una base de datos de combinaciones recomendadas cargada externamente.2 Este archivo .ts engloba toda esta complejidad lógica, devolviendo un objeto DimItem limpio y tipado que los componentes .tsx pueden mostrar sin necesidad de realizar cálculos matemáticos.

### **1.3 Construcción de las Tiendas: d2-store-factory.ts**

Los ítems no flotan en el vacío; pertenecen a una entidad. El archivo src/app/inventory/store/d2-store-factory.ts se encarga de crear los objetos DimStore que representan a los personajes (Titán, Cazador, Hechicero) y al Depósito (Vault).

Este módulo procesa los datos de characters de la API para determinar atributos vitales como el nivel de luz base (sin bono de artefacto), el emblema de fondo, y la fecha de la última sesión de juego. Crucialmente, asigna los ítems procesados por la fábrica de ítems a sus respectivas tiendas. Aquí se establece la relación de pertenencia: cada DimStore contiene un array de items.

La lógica dentro de d2-store-factory.ts también maneja la clasificación inicial de los ítems en "buckets" o categorías. Aunque la API de Bungie devuelve una lista plana, DIM organiza internamente los ítems en estructuras lógicas (ej. Cinética, Energética, Pesada) para facilitar su renderizado posterior en columnas segregadas.

### **1.4 Gestión de Estado con Redux: actions.ts y reducers**

Todo este flujo de datos culmina en el sistema de gestión de estado de Redux. Los archivos actions.ts definen los tipos de eventos que pueden ocurrir (ej. LOAD\_STORES, UPDATE\_STORES, ITEM\_MOVED). Los reducers, funciones puras ubicadas típicamente en reducer.ts dentro del directorio de inventario, escuchan estas acciones y actualizan el estado global de la aplicación.

Cuando d2-stores.ts termina de procesar los datos, despacha una acción update({ stores }). El reducer toma este nuevo array de tiendas y reemplaza la versión anterior en el estado. Es fundamental notar que esta operación es inmutable; se crean nuevas referencias de objetos para que React pueda detectar eficientemente que los datos han cambiado y desencadenar una actualización de la interfaz. Este patrón de diseño asegura que la vista siempre esté sincronizada con los datos subyacentes, permitiendo características como la actualización en tiempo real cuando se mueve un ítem en el juego o mediante otra aplicación.3

## ---

**2\. La Capa de Presentación: Jerarquía de Componentes y Englobamiento (.tsx)**

Una vez que los datos han sido extraídos, procesados y almacenados en Redux, entra en acción la capa de presentación. Los archivos .tsx en src/app/inventory definen la estructura visual y la interactividad. La arquitectura sigue un patrón de "muñecas rusas", donde componentes contenedores de alto nivel engloban a componentes cada vez más específicos.

### **2.1 El Orquestador Visual: Inventory.tsx**

El archivo src/app/inventory/Inventory.tsx es el punto de entrada principal para la pantalla de inventario.1 Este componente no se preocupa por cómo renderizar un icono de arma; su responsabilidad es la orquestación de alto nivel y el diseño estructural de la página (layout).

Al montarse, Inventory.tsx utiliza hooks de React-Redux (useSelector) para suscribirse a los selectores de datos (storesSelector). Si los datos no están listos, renderiza un componente de carga (Loading). Una vez que los datos están disponibles, su función principal es establecer el contenedor de desplazamiento horizontal que alojará a los personajes.

Este componente también actúa como el límite de error (ErrorBoundary) para la sección de inventario y gestiona la visualización de componentes globales superpuestos, como el panel de detalles de carga (LoadoutDrawer) o las notificaciones de arrastrar y soltar. Es el "padre" que engloba todo el contexto necesario para que los hijos funcionen.

### **2.2 La Estructura de Columnas: Stores.tsx**

Descendiendo un nivel en la jerarquía, encontramos src/app/inventory/Stores.tsx. Este componente recibe la lista de stores (personajes \+ depósito) como una propiedad (prop). Su trabajo es iterar sobre esta lista y generar una columna visual para cada entidad.3

El método de renderizado de Stores.tsx es donde se define la "geografía" del inventario. Utiliza un contenedor flexible (Flexbox) o una cuadrícula (Grid) CSS para alinear las columnas de los personajes una al lado de la otra. Es aquí donde se toma la decisión de diseño de mostrar el Depósito (Vault) como una columna más, aunque a menudo con un estilo visualmente distinto para diferenciarlo de los guardianes activos.

Para cada tienda en la lista, Stores.tsx instancia un componente contenedor de columna, pasando el objeto store completo hacia abajo. Este paso de "props drilling" (pasar propiedades hacia abajo) es controlado, asegurando que cada columna solo tenga acceso a los datos de su dueño específico.

### **2.3 Identidad y Contexto: StoreHeading.tsx**

En la parte superior de cada columna generada por Stores.tsx reside el componente StoreHeading.tsx. Este componente tiene la responsabilidad de representar la identidad del personaje. Renderiza el emblema de fondo (emblemBackgroundPath), la clase del personaje (Titán, Cazador, Hechicero) y, crucialmente, el nivel de luz (Power Level) calculado.

StoreHeading.tsx no es solo decorativo; es interactivo. Engloba menús desplegables (StoreHeadingContextMenu) que permiten al usuario ejecutar acciones a nivel de personaje, como "Maximizar Poder", "Recoger del Postmaster" o iniciar el "Modo Farming".4 Este componente demuestra cómo la lógica (.ts) y la vista (.tsx) se entrelazan: el componente visual llama a funciones de acción importadas de los archivos de lógica para modificar el estado de la aplicación.

### **2.4 Organización Categórica: StoreBucket.tsx**

Debajo del encabezado, el inventario no es una lista caótica. Stores.tsx llama repetidamente a StoreBucket.tsx para cada categoría de ítem definida en el juego (Cinética, Energética, Pesada, Casco, Guanteletes, etc.).

StoreBucket.tsx actúa como un contenedor de agrupación lógica. Recibe el store y el bucketId. Su lógica interna filtra la lista de ítems del personaje para encontrar solo aquellos que pertenecen a ese cubo específico. Este componente es fundamental para la experiencia de usuario, ya que crea las "cajas" visuales donde viven los ítems.

Además de agrupar, StoreBucket.tsx es una pieza clave en el sistema de "Arrastrar y Soltar" (Drag and Drop). Cada bucket se registra como una "zona de caída" (DropZone). Cuando el usuario arrastra un ítem, el sistema verifica si el StoreBucket sobre el que se encuentra el cursor es compatible con el tipo de ítem arrastrado (ej. no permite soltar un casco en el cubo de armas cinéticas).

### **2.5 La Unidad Atómica: ConnectedInventoryItem.tsx y InventoryItem.tsx**

En el nivel más bajo de la jerarquía visual, dentro de cada StoreBucket, encontramos los ítems individuales. Aquí DIM emplea un patrón de rendimiento crítico: la separación entre ConnectedInventoryItem y InventoryItem.

ConnectedInventoryItem.tsx es un componente contenedor ("wrapper") que se conecta al store de Redux. Recibe un identificador de ítem (itemHash o id) y es responsable de extraer los datos actualizados de ese ítem específico del estado global. Este patrón evita que los componentes padres (Stores o Inventory) tengan que re-renderizarse completamente si solo cambia un atributo menor de un solo ítem.3

Una vez obtenidos los datos, ConnectedInventoryItem renderiza a InventoryItem.tsx, que es un componente "presentacional" puro. InventoryItem.tsx es responsable de dibujar el cuadrado del ítem:

* **Icono**: La imagen principal del ítem.  
* **Bordes**: Colores que indican rareza (Exótico, Legendario) o estado de Obra Maestra (borde dorado).  
* **Superposiciones (Overlays)**: Pequeños iconos que indican el elemento (Solar, Arco, Vacío), la temporada de origen (marca de agua), si tiene borde rojo (Deepsight) o si ha sido fabricado (Crafted).  
* **Tags**: Iconos definidos por el usuario (favorito, basura, infundir) que se superponen en las esquinas.

Este componente engloba toda la complejidad visual de un ítem en un paquete reutilizable. Cada vez que se ve un ítem en DIM (ya sea en el inventario, en los vendedores o en el buscador de loadouts), es muy probable que sea una instancia de InventoryItem.tsx.

## ---

**3\. Estilizado y Arquitectura Visual Modular (.m.scss vs.css)**

La solicitud requiere una investigación sobre los archivos .css y .m.scss y cómo funcionan en conjunto. DIM utiliza una estrategia híbrida que combina estilos globales con módulos de CSS encapsulados para mantener la mantenibilidad en una base de código masiva.

### **3.1 CSS Modules (.m.scss): Encapsulamiento y Seguridad**

La mayoría de los componentes de React en el inventario tienen un archivo de estilo correspondiente con la extensión .m.scss (Sass Modules). Por ejemplo, InventoryItem.tsx importa estilos de InventoryItem.m.scss.

La magia de los archivos .m.scss radica en cómo se procesan durante la compilación. A diferencia del CSS tradicional, donde una clase llamada .icon podría entrar en conflicto con otra clase .icon en un componente diferente, los Módulos CSS garantizan la unicidad local.

Cuando InventoryItem.tsx hace:

TypeScript

import styles from './InventoryItem.m.scss';  
//...  
\<div className\={styles.icon}\>...\</div\>

El sistema de construcción transforma el nombre de la clase .icon en algo único y hash, como InventoryItem\_icon\_\_3x9Gz. Esto significa que el desarrollador puede usar nombres de clase simples y semánticos (container, header, text) dentro de cada componente sin preocuparse por afectar al resto de la aplicación globalmente. Este "englobamiento" de estilos es paralelo al englobamiento de lógica en los componentes de React.

### **3.2 Sass y Preprocesamiento: Variables y Mixins**

La extensión .scss indica el uso de Sass, un preprocesador de CSS. Esto permite a DIM utilizar características avanzadas de programación en sus hojas de estilo:

* **Variables**: Definidas en archivos compartidos (ej. \_variables.scss), almacenan valores como los colores oficiales de rareza de Destiny ($exotic-color: \#f5dc56), tamaños de fuente estándar y puntos de ruptura para diseño responsivo.  
* **Mixins**: Bloques de código CSS reutilizables. Por ejemplo, un mixin para el efecto de "brillo" al pasar el mouse por encima de un ítem se puede definir una vez e incluir en múltiples componentes de ítem diferentes (@include item-hover-effect).  
* **Anidamiento (Nesting)**: Permite escribir selectores CSS que reflejan la jerarquía HTML del componente, mejorando la legibilidad (ej. .container {.icon {... } }).

### **3.3 Estilos Globales (.css)**

A pesar de la preferencia por los módulos, DIM utiliza archivos .css estándar para estilos que *deben* ser globales. El archivo global.css (o similar) define:

* **Variables CSS Nativas (--variables)**: Utilizadas para el soporte de temas dinámicos (Modo Oscuro, Modo Claro). Los componentes .m.scss consumen estas variables (ej. color: var(--theme-text-primary)). Cuando el usuario cambia el tema en la configuración, solo cambian los valores de estas variables globales, y toda la interfaz se actualiza instantáneamente sin re-renderizar la estructura React.  
* **Normalización**: Estilos base para asegurar que los elementos HTML estándar (div, span, input) se comporten de manera consistente en diferentes navegadores (Chrome, Firefox, Safari).

## ---

**4\. Subsistemas Avanzados y Su Integración**

El inventario no es una isla; está conectado a subsistemas complejos que reutilizan sus componentes y lógica.

### **4.1 El Optimizador de Loadouts (LoadoutOptimizer.tsx)**

Ubicado en src/app/loadout/LoadoutOptimizer.tsx, este subsistema es una pieza de ingeniería computacional avanzada.7

* **Extracción de Datos**: No vuelve a llamar a la API. Utiliza los selectores de inventario (storesSelector) para obtener todos los ítems de armadura ya cargados en memoria.  
* **Algoritmo y Web Workers**: El cálculo de combinaciones de armadura para alcanzar estadísticas específicas (T100, T90) es una tarea intensiva (problema de la mochila/Knapsack problem). Para evitar congelar la interfaz de usuario (el hilo principal), DIM descarga este procesamiento a Web Workers (loadout-optimizer.worker.ts). El componente .tsx envía los datos de los ítems y los parámetros de filtrado al worker, y se suscribe a los mensajes de respuesta que contienen las combinaciones válidas.  
* **Representación**: Una vez recibidas las combinaciones, reutiliza InventoryItem.tsx para mostrar qué piezas componen cada set, y utiliza gráficos SVG personalizados para visualizar la distribución de estadísticas (tier bars).

### **4.2 El Organizador (Organizer.tsx)**

El Organizador (src/app/organizer/Organizer.tsx) ofrece una vista tabular plana del inventario, rompiendo la jerarquía de "Personaje \-\> Bucket".9

* **Aplanamiento de Datos**: Toma la estructura jerárquica del inventario y la aplana en una sola matriz de todos los ítems.  
* **Tabla Virtualizada**: Dado que un jugador puede tener 600 ítems, renderizar 600 filas de tabla simultáneamente destruiría el rendimiento. El Organizador utiliza técnicas de virtualización (posiblemente librerías como react-window) para renderizar solo las filas visibles en la pantalla, reciclando los componentes DOM a medida que el usuario se desplaza.  
* **Columnas Dinámicas**: Permite al usuario seleccionar qué atributos ver. Esto requiere una lógica de mapeo dinámica donde el componente de la tabla itera sobre una configuración de columnas y extrae el valor correspondiente de cada objeto DimItem (ej. item.stats.find(s \=\> s.statHash \=== TARGET\_HASH).value).

### **4.3 Integración de Vendedores (Vendors.tsx)**

El módulo de Vendedores (src/app/vendors/Vendors.tsx) demuestra la reutilización de código.1

* **Abstracción de Datos**: Los ítems que venden los NPCs (Banshee, Ada-1) se procesan utilizando la misma d2-item-factory.ts que los ítems del jugador. Esto permite que se representen como objetos DimItem, aunque con propiedades adicionales como cost (costo de compra) y failureStrings (por qué no puedes comprarlo).  
* **Componente Híbrido (VendorItem.tsx)**: Aunque reutiliza lógica, la visualización difiere ligeramente (necesita mostrar el precio). Por lo tanto, existe un componente VendorItem.tsx que envuelve o extiende la funcionalidad básica de visualización de ítems, añadiendo la capa de información de costos e inspección de ventajas específica para vendedores.

## ---

**5\. Análisis de Flujo de Llamadas y "Englobamiento" (Call Graph Narrative)**

Para sintetizar "cómo uno llama a otro", narraremos el ciclo de vida completo de una interacción común:

1. **Inicio (Bootstrap)**: d2-stores.ts (TS) llama a la API, procesa datos con d2-item-factory.ts (TS) y despacha acciones a Redux.  
2. **Actualización de Estado**: Redux actualiza el "store" global.  
3. **Reacción Visual**: Inventory.tsx (TSX) detecta el cambio de estado.  
4. **Cascada de Renderizado**:  
   * Inventory.tsx renderiza \<Stores /\>.  
   * \<Stores /\> itera y renderiza 3 \<div className={column}\> conteniendo \<StoreHeading /\> y múltiples \<StoreBucket /\>.  
   * Cada \<StoreBucket /\> filtra ítems y renderiza múltiples \<ConnectedInventoryItem /\>.  
   * \<ConnectedInventoryItem /\> lee del estado y renderiza \<InventoryItem /\>.  
   * \<InventoryItem /\> aplica clases de InventoryItem.m.scss para dibujarse.  
5. **Interacción de Usuario (Click)**:  
   * Usuario hace clic en el ítem. InventoryItem.tsx dispara props.onClick.  
   * El manejador abre \<ItemPopup /\>.  
   * \<ItemPopup /\> carga \<ItemDetails /\>, que a su vez carga \<ItemSockets /\>.  
   * \<ItemSockets /\> itera sobre los perks del ítem y renderiza \<Socket /\>.  
   * \<Socket /\> renderiza \<Plug /\>, mostrando la imagen del perk.

Esta cadena demuestra un acoplamiento laxo pero altamente cohesivo, donde cada eslabón de la cadena tiene una responsabilidad única y definida, permitiendo a DIM escalar para manejar la inmensa complejidad del ecosistema de Destiny 2\.

## **Tabla de Datos Estructurada: Comparativa de Componentes y Responsabilidades**

A continuación se presenta una tabla que resume la función técnica y el tipo de archivo de los componentes clave analizados en la investigación.

| Componente / Archivo | Tipo (.ext) | Responsabilidad Principal | Dependencias Clave |
| :---- | :---- | :---- | :---- |
| d2-stores.ts | Lógica (.ts) | Extracción de datos de API, orquestación de carga. | bungie-api-ts, redux |
| d2-item-factory.ts | Lógica (.ts) | Transformación de JSON crudo a objetos DimItem. | D2ManifestDefinitions |
| Inventory.tsx | Vista (.tsx) | Layout principal, manejo de estado de carga, inyección de dependencias. | Stores, LoadoutDrawer |
| Stores.tsx | Vista (.tsx) | Renderizado de columnas de personajes y grid. | StoreHeading, StoreBucket |
| StoreBucket.tsx | Vista (.tsx) | Agrupación lógica de ítems (ej. Armas Cinéticas). DropZone para DnD. | ConnectedInventoryItem |
| InventoryItem.tsx | Vista (.tsx) | Renderizado atómico del ítem (iconos, bordes, overlays). | InventoryItem.m.scss |
| LoadoutOptimizer.tsx | Subsistema (.tsx) | Cálculo matemático de builds óptimas. | Web Workers, DimItem |
| InventoryItem.m.scss | Estilo (.m.scss) | Estilos encapsulados para el ítem (iconos, capas z-index). | Variables globales Sass |
| global.css | Estilo (.css) | Variables de tema CSS, normalización, estilos base. | N/A |

## ---

**6\. Conclusión de la Investigación**

La investigación revela que el sistema de inventario de DIM es una maquinaria sofisticada diseñada para resolver un problema de gestión de datos masivos en el navegador. No se trata simplemente de mostrar imágenes; es un sistema operativo completo para ítems de juego que emula una base de datos relacional en memoria (Redux) y proyecta esos datos a través de una jerarquía de componentes React altamente optimizada.

El uso de **TypeScript** es el pegamento que mantiene la integridad del sistema, permitiendo que la lógica de negocio compleja (.ts) se comunique de forma segura con la capa visual (.tsx). La estrategia de **Sass Modules (.m.scss)** resuelve el problema de escalabilidad en el diseño, permitiendo que cientos de componentes coexistan sin conflictos visuales. Finalmente, la arquitectura de **"Englobamiento"** (componentes contenedores conectados a Redux que envuelven a componentes presentacionales puros) asegura que la aplicación siga siendo rápida y reactiva, incluso cuando gestiona miles de ítems simultáneamente.

#### **Obras citadas**

1. DestinyItemManager/DIM: Destiny Item Manager \- GitHub, fecha de acceso: diciembre 29, 2025, [https://github.com/DestinyItemManager/DIM](https://github.com/DestinyItemManager/DIM)  
2. Destiny Item Manager, fecha de acceso: diciembre 29, 2025, [https://destinyitemmanager.com/en/](https://destinyitemmanager.com/en/)  
3. DIM/src/app/inventory/d1-stores.ts at master · DestinyItemManager/DIM \- GitHub, fecha de acceso: diciembre 29, 2025, [https://github.com/DestinyItemManager/DIM/blob/master/src/app/inventory/d1-stores.ts](https://github.com/DestinyItemManager/DIM/blob/master/src/app/inventory/d1-stores.ts)  
4. Inventory · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 29, 2025, [https://github.com/DestinyItemManager/DIM/wiki/Inventory](https://github.com/DestinyItemManager/DIM/wiki/Inventory)  
5. Destiny2.GetProfile | BungieNetPlatform \- GitHub Pages, fecha de acceso: diciembre 29, 2025, [http://destinydevs.github.io/BungieNetPlatform/docs/services/Destiny2/Destiny2-GetProfile](http://destinydevs.github.io/BungieNetPlatform/docs/services/Destiny2/Destiny2-GetProfile)  
6. Understand Redux State Management: The Department Store analogy \- DEV Community, fecha de acceso: diciembre 29, 2025, [https://dev.to/cathylai/understand-redux-state-management-the-department-store-analogy-4e4p](https://dev.to/cathylai/understand-redux-state-management-the-department-store-analogy-4e4p)  
7. Loadout Optimizer · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 29, 2025, [https://github.com/DestinyItemManager/DIM/wiki/Loadout-Optimizer](https://github.com/DestinyItemManager/DIM/wiki/Loadout-Optimizer)  
8. Loadout Optimizer · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 29, 2025, [https://github.com/DestinyItemManager/DIM/wiki/Loadout-Optimizer/a4839c9bacbca2984b52d488de18189efcaac635](https://github.com/DestinyItemManager/DIM/wiki/Loadout-Optimizer/a4839c9bacbca2984b52d488de18189efcaac635)  
9. Organizer · DestinyItemManager/DIM Wiki \- GitHub, fecha de acceso: diciembre 29, 2025, [https://github.com/DestinyItemManager/DIM/wiki/Organizer](https://github.com/DestinyItemManager/DIM/wiki/Organizer)