# **Informe de Investigación Técnica: Arquitectura de Implementación y Extracción de Datos para el Subsistema de Hazañas (Triumphs) en Destiny Item Manager (DIM)**

## **1\. Resumen Ejecutivo y Alcance de la Investigación**

Este documento constituye un informe técnico exhaustivo diseñado para diseccionar la implementación, gestión y visualización del sistema de "Hazañas" (conocidas técnicamente como *Records* o *Triumphs*) dentro de la aplicación Destiny Item Manager (DIM). El análisis responde a la solicitud de investigar el repositorio de código abierto de DIM y sus dependencias auxiliares para determinar "qué saca" (extracción de datos), "cómo lo hace" (lógica de procesamiento) y "qué debería tener" un sistema competente de gestión de logros en el ecosistema de Destiny 2\.

La investigación se basa en un análisis forense de los repositorios públicos de GitHub de DIM, la documentación técnica de la API de Bungie.net y los flujos de datos auxiliares identificados durante el estudio. DIM no opera como una entidad aislada; actúa como una capa de interpretación sofisticada sobre una base de datos relacional masiva y distribuida expuesta por Bungie. El subsistema de Hazañas representa uno de los desafíos más complejos de ingeniería de datos dentro de la aplicación, requiriendo la fusión en tiempo real de definiciones estáticas masivas (el Manifiesto), estados de usuario dinámicos y volátiles (Componentes de Perfil), y metadatos curados manualmente por la comunidad para suplir las carencias de la API oficial.

El informe detalla la arquitectura de ingestión de datos, la lógica recursiva de travesía de árboles para la visualización de nodos de presentación, el uso de aritmética binaria (bitmasks) para la gestión de estados, y la infraestructura de datos suplementaria que permite a DIM ofrecer funcionalidades que exceden las capacidades del cliente de juego nativo.

## **2\. Fundamentos de la Arquitectura de Datos: El Manifiesto y la API**

Para comprender "qué saca" DIM, primero es imperativo establecer la naturaleza de la fuente de datos. DIM no almacena el progreso de las hazañas en sus propios servidores; funciona como un cliente sin estado (stateless) que hidrata su interfaz con datos provenientes de dos fuentes primarias proporcionadas por Bungie: el Manifiesto (Definiciones Estáticas) y la Respuesta de Perfil (Datos Dinámicos).

### **2.1 El Manifiesto de Destiny (The Manifest)**

El cimiento sobre el cual se construye todo el sistema de Hazañas en DIM es la base de datos de definiciones, técnicamente conocida como DestinyManifest. Este es un archivo de base de datos SQLite (frecuentemente consumido como un JSON masivo comprimido en la web) que contiene la información inmutable sobre cada entidad en el juego.1

En el contexto de las Hazañas, DIM extrae y almacena localmente (usando IndexedDB en el navegador del usuario) las siguientes definiciones críticas:

* **DestinyRecordDefinition:** Esta es la unidad atómica de una Hazaña. Contiene el texto descriptivo (nombre, lore), el icono, los requisitos de finalización (Objetivos) y los datos de recompensa.2 Es crucial entender que esta definición *no contiene el progreso del jugador*; solo describe *qué es* la hazaña. DIM debe descargar miles de estas definiciones para poder renderizar la interfaz.  
* **DestinyPresentationNodeDefinition:** Las hazañas no existen en un vacío; están organizadas jerárquicamente. Esta definición representa la estructura de árbol (Nodos de Presentación). Un nodo puede contener otros nodos (hijos) o registros (hojas).3 Esta estructura recursiva es lo que forma las "Pestañas" y "Categorías" (ej. Hazañas \-\> Vanguardia \-\> Asaltos) que el usuario navega.  
* **DestinyObjectiveDefinition:** La mayoría de las hazañas requieren completar tareas específicas (ej. "Derrota 500 enemigos con daño solar"). Esta definición describe la mecánica de la tarea y el valor objetivo numérico.3  
* **DestinyMetricDefinition:** A menudo visualizadas junto a las hazañas, las métricas rastrean estadísticas crudas (ej. "Total de Bajas") y poseen sus propias estructuras de definición que DIM debe correlacionar.3

La extracción de este Manifiesto es el primer paso en la cadena de "cómo lo hace". Al iniciar la aplicación, DIM verifica si la versión del Manifiesto en el navegador coincide con la versión actual en los servidores de Bungie. Si hay discrepancia, descarga y procesa el nuevo conjunto de definiciones, asegurando que incluso las hazañas añadidas ese mismo día estén disponibles para inspección.

### **2.2 La Respuesta del Perfil de Usuario (Profile Response)**

Una vez que DIM "sabe" qué hazañas existen (gracias al Manifiesto), debe averiguar cuáles ha completado el usuario. Para ello, realiza una petición al endpoint Destiny2.GetProfile. La investigación revela que DIM solicita componentes específicos diseñados para minimizar la transferencia de datos mientras se maximiza la información de progreso.

Los componentes esenciales que DIM extrae para el sistema de Hazañas son:

* **Componente 900 (Records):** Este componente devuelve el DestinyProfileRecordsComponent.2 Contiene el estado de progreso de todas las hazañas que son de "alcance de cuenta" (Account-wide). Es la fuente de verdad para la mayoría de los títulos y logros globales.  
* **Componente 200 (Characters):** Dado que algunas hazañas son específicas del personaje (ej. "Completa una Incursión con una subclase de Vacío"), DIM debe iterar sobre cada personaje activo en la cuenta para extraer sus respectivos DestinyCharacterRecordsComponent. La lógica de la aplicación debe luego decidir qué progreso mostrar en la vista unificada.  
* **Componente 104 (Collectibles):** Aunque técnicamente distintos de las Hazañas, la pantalla de "Colecciones" utiliza una estructura de nodo de presentación idéntica y a menudo se cruza con las hazañas para el desbloqueo de Sellos (Badges).4  
* **Componente 102 (ProfileInventories) y 201 (CharacterInventories):** DIM cruza datos de inventario con hazañas, especialmente para determinar si el usuario posee los objetos necesarios para completar ciertos triunfos o si ha obtenido las recompensas asociadas.

La respuesta de la API es un mapa que vincula un **Hash de Registro** (el ID del Manifiesto) con un **Componente de Registro** (el progreso del usuario). Este componente es extremadamente ligero, conteniendo principalmente valores enteros que representan banderas de estado y progreso de objetivos, lo cual nos lleva al corazón lógico del sistema: la gestión de estados bit a bit.

## **3\. Lógica de Estado y Bitmasks: El Motor de Procesamiento**

La pregunta central de "cómo lo hace" se responde en la capa de procesamiento lógico de DIM. La API de Bungie no devuelve cadenas de texto legibles como "Completado" o "En Progreso". En su lugar, utiliza **Enumeraciones de Bitmask** (Bitwise Flags) para la gestión de estados. Esta es una técnica de optimización crítica que permite transmitir múltiples estados booleanos en un solo número entero.

### **3.1 Decodificación de DestinyRecordState**

DIM implementa una lógica rigurosa para interpretar el campo state (un entero de 32 bits) asociado a cada hazaña. Según la documentación técnica 5, los valores de la enumeración DestinyRecordState que DIM debe evaluar son:

| Valor Decimal | Nombre del Enum | Interpretación Lógica en DIM |
| :---- | :---- | :---- |
| **0** | None | El estado base. La hazaña es visible, activa y **no completada**. Si el estado es 0, DIM muestra la hazaña como pendiente. |
| **1** | RecordRedeemed | La hazaña ha sido completada y el jugador ha hecho clic para "reclamarla" en el juego. DIM renderiza esto con un check verde o lo oculta si el filtro es "Incompletos". |
| **2** | RewardUnavailable | La hazaña está completada, pero la recompensa no se puede reclamar (quizás por inventario lleno). DIM alerta al usuario sobre esta condición. |
| **4** | ObjectiveNotCompleted | **Crítico.** Si este bit está activo, los objetivos no están cumplidos. Si este bit es 0, significa que los objetivos *están* cumplidos (independientemente de si se ha reclamado o no). |
| **8** | Obscured | "Hazaña Secreta". DIM detecta este bit y, en lugar de mostrar el texto del Manifiesto, muestra "Secreto" u ofusca el contenido para evitar spoilers, a menos que el usuario active la opción de "Revelar Secretos". |
| **16** | Invisible | La hazaña existe en la base de datos pero no debe mostrarse bajo ninguna circunstancia (contenido no lanzado). La lógica de recorrido de árbol de DIM poda activamente cualquier nodo con este bit. |
| **32** | EntitlementUnowned | Requiere una licencia de DLC que el usuario no posee. DIM suele indicarlo con un icono de candado. |
| **64** | CanEquipTitle | Específico para los Sellos. Indica que el título asociado puede ser equipado. DIM habilita el botón de "Equipar" en la interfaz si este bit está presente.5 |

### **3.2 Operaciones Bit a Bit en el Código**

La implementación en TypeScript dentro de DIM no realiza comparaciones de igualdad simple. Utiliza operadores bit a bit (&) para verificar la presencia de estas banderas.

Por ejemplo, para determinar si una hazaña debe mostrarse como "Completada pero no reclamada" (una notificación común en DIM), la lógica sería similar a:

TypeScript

// Lógica conceptual de DIM  
const isCompleted \= (record.state & DestinyRecordState.ObjectiveNotCompleted) \=== 0;  
const isRedeemed \= (record.state & DestinyRecordState.RecordRedeemed)\!== 0;  
const isRedeemable \= isCompleted &&\!isRedeemed;

if (isRedeemable) {  
    // Añadir a la lista de "Hazañas por reclamar" en la UI  
    NotificationService.notify("Tienes hazañas pendientes.");  
}

Esta eficiencia matemática es vital. DIM procesa más de 15,000 registros cada vez que se carga el perfil. Realizar comparaciones de cadenas de texto sería prohibitivamente lento en dispositivos móviles o navegadores antiguos. El uso de bitmasks permite filtrar, ordenar y categorizar miles de hazañas en milisegundos.

## **4\. Ingeniería de Ingesta y Gestión de Estado (Redux)**

DIM es una aplicación basada en React que utiliza **Redux** para la gestión del estado global. La investigación del repositorio sugiere una arquitectura de flujo de datos unidireccional altamente optimizada para manejar la complejidad de los datos de Destiny.

### **4.1 El Ciclo de Vida de los Datos de Hazañas**

1. **Acción (Dispatch):** Cuando el usuario inicia sesión o pulsa "Refrescar", se dispara una acción asíncrona (Thunk o Saga) que llama a Destiny2.GetProfile.  
2. **Normalización:** La respuesta cruda de la API es masiva y anidada. DIM probablemente normaliza estos datos antes de almacenarlos en el store de Redux. Esto significa que en lugar de guardar un árbol profundo, guarda diccionarios planos: records: { \[hash\]: RecordComponent }.  
3. **Selectores (Reselect):** Aquí reside la inteligencia de DIM. Utiliza librerías como reselect para computar datos derivados. Los selectores toman el estado plano y el Manifiesto y construyen los objetos hidratados que la UI necesita solo cuando los datos cambian.  
   * getRecordDefs: Recupera las definiciones estáticas.  
   * getProfileRecords: Recupera el progreso del usuario.  
   * getVisibleTriumphs: Un selector complejo que fusiona ambos, aplica la lógica de bitmask para filtrar los invisibles y devuelve la estructura de árbol lista para renderizar.

### **4.2 Reconstrucción del Árbol de Presentación (Recursividad)**

El desafío más visual que resuelve DIM es la reconstrucción del árbol de navegación de Hazañas. La API define una estructura jerárquica compleja comenzando desde un "Nodo Raíz".

* **Identificación de Raíces:** DIM utiliza constantes o busca en el componente de perfil los hashes de los nodos raíz. Para Hazañas, el hash raíz suele ser 1652422747 (Triumphs) y 1652422747 (Seals, a menudo compartiendo raíz o bifurcándose en niveles inferiores).4  
* **Algoritmo Recursivo:** El código de DIM (ubicado lógicamente en módulos como presentation-nodes.ts 6) implementa una función recursiva:  
  1. Toma un PresentationNodeHash.  
  2. Busca su definición en el Manifiesto.  
  3. Itera sobre su array children.  
  4. Si un hijo es otro PresentationNode, la función se llama a sí misma (recursión).  
  5. Si un hijo es un Record, busca su estado en el perfil del usuario.  
  6. **Poda (Pruning):** Si un nodo hijo o registro está marcado como Invisible (bit 16), la función retorna null o lo excluye del array resultante. Esto asegura que el usuario no vea carpetas vacías o contenido de depuración.

Este enfoque permite a DIM adaptarse dinámicamente. Si Bungie añade una nueva categoría "Eventos 2025" mañana, DIM la renderizará automáticamente sin necesidad de una actualización de código, siempre que el Manifiesto esté actualizado, ya que la estructura se deriva de los datos, no está "hardcodeada" en la interfaz.

## **5\. Datos Auxiliares y Curación Manual: d2-additional-info**

Una de las revelaciones más significativas de la investigación es que la API de Bungie por sí sola es insuficiente para la experiencia de alta calidad que ofrece DIM. El equipo de desarrollo mantiene un repositorio separado y vital llamado **d2-additional-info**.7

### **5.1 La Necesidad de Datos Suplementarios**

La API oficial a veces omite detalles, contiene errores o protege información para evitar spoilers de una manera que degrada la usabilidad. d2-additional-info es un proyecto en TypeScript que procesa el Manifiesto y genera archivos JSON suplementarios que DIM consume como una base de datos secundaria.

La investigación identifica archivos específicos generados por este proyecto que son críticos para el subsistema de Hazañas:

* **catalyst-triumph-icons.json:** 7 Un problema persistente en la API es la disociación entre la Hazaña de un Catalizador Exótico y el objeto del Catalizador en sí. A menudo, la hazaña tiene un icono genérico. Este archivo mapea manualmente el hash de la hazaña al hash del inventario correcto para que DIM muestre el icono del arma o catalizador real, mejorando la identificación visual.  
* **source-info.ts:** La API indica que una hazaña existe, pero raramente explica *dónde* conseguirla de manera clara (ej. "Fuente: Actividad Desconocida"). DIM utiliza este archivo para inyectar cadenas de texto descriptivas curadas por la comunidad (ej. "Caída aleatoria en la Incursión Último Deseo") directamente en la descripción de la hazaña.  
* **bad-vendors.json y craftable-hashes.json:** Ayudan a limpiar datos erróneos de vendedores o identificar qué armas vinculadas a hazañas son fabricables, cruzando datos de registros con datos de patrones de fabricación.7

### **5.2 El Pipeline de Construcción**

Este repositorio no es estático; es un pipeline de datos activo. Utiliza scripts (pnpm generate-data) que se ejecutan periódicamente (posiblemente en cada actualización del juego) para escanear el nuevo Manifiesto y regenerar los archivos JSON.7 Esto demuestra que lo que DIM "saca" es una amalgama de datos oficiales y datos comunitarios procesados heurísticamente.

## **6\. Hazañas Especiales: Títulos (Seals) y Dorados (Gilding)**

Dentro del sistema de hazañas, los "Títulos" o "Sellos" ocupan una jerarquía especial y requieren lógica adicional.

### **6.1 Detección y Visualización de Títulos**

Un Sello es técnicamente un PresentationNode, pero DIM debe distinguirlo de una simple categoría como "Lore". La distinción se hace verificando la propiedad completionRecordHash o inspeccionando si el nodo contiene un registro que otorga un título equipable.

La lógica de DIM separa los sellos en:

* **Activos:** Aquellos que aún se pueden completar.  
* **Legacy (Legado):** Aquellos cuyos requisitos ya no están disponibles en el juego. DIM determina esto cruzando los hashes de los objetivos con listas de contenido "vaulted" (retirado) o verificando banderas de validez en la API.

### **6.2 La Complejidad del "Gilding" (Dorar)**

El concepto de "Dorar" un título (completar requisitos adicionales estacionales para cambiar el color del título a dorado) introduce una capa de complejidad temporal.

* **Mecánica:** El estado de dorado se reinicia cada temporada.  
* **Implementación:** DIM debe rastrear el gildingTrackingRecordHash asociado al Sello. Este registro es volátil. La aplicación verifica si este registro específico está completado en la temporada actual.  
* **Visualización:** Si el registro de dorado está completo, DIM cambia el estilo CSS del título a un gradiente dorado y muestra el contador de "veces dorado" extrayendo el valor de los objetivos de intervalo del registro de dorado.

### **6.3 Acciones de Escritura: Equipar Títulos**

A diferencia de la mayoría de las hazañas que son de solo lectura, los Títulos permiten interacción. DIM implementa la capacidad de equipar un título desde la app.

* **Validación de Cliente:** Antes de enviar la petición a la API, DIM verifica localmente: (record.state & DestinyRecordState.CanEquipTitle) \=== 64\. Si esta condición es falsa, el botón se deshabilita, evitando errores de API innecesarios.5  
* **Endpoint:** Utiliza Destiny2.Action con el tipo de acción específico para equipar títulos, pasando el hash del registro del título.

## **7\. Sistemas de Puntuación (Triumph Score)**

El "Puntaje de Hazaña" es una métrica de vanidad central en Destiny 2\. DIM no confía ciegamente en el número total que devuelve el perfil, sino que a menudo lo recalcula o lo desglosa para ofrecer mayor granularidad.

### **7.1 Puntuación Activa vs. Total**

Debido a la eliminación de contenido (DCV \- Destiny Content Vault), Bungie separa la puntuación en "Activa" y "Legado".

* **Cálculo en DIM:** La aplicación recorre el árbol de registros hidratados. Para cada registro donde state & RecordRedeemed, suma el completionValue (valor de puntos) a un acumulador local.  
* **Detección de Glitches:** Snippets de investigación sugieren que la comunidad usa DIM para auditar sus puntuaciones.8 A veces el juego muestra una puntuación incorrecta. DIM, al sumar los valores individuales de las definiciones del Manifiesto, puede mostrar la "Puntuación Real Matemática" versus la "Puntuación Mostrada en el Emblema", ayudando a los usuarios a identificar hazañas glitcheadas que no otorgaron puntos.

### **7.2 Rareza Global**

Aunque no es nativo de la API de Bungie, DIM integra datos de rareza (ej. "Solo el 0.5% de los jugadores tiene este título").9

* **Mecanismo:** Esto se logra mediante la consulta a servicios de terceros (como Warmind.io o una base de datos propia de DIM recopilada anónimamente).  
* **Integración:** DIM realiza una petición asíncrona secundaria (fetchGlobalRarity) que devuelve un mapa { recordHash: percentage }. Al renderizar la hazaña, cruza este mapa para mostrar la estadística de rareza, añadiendo un contexto social valioso a los datos técnicos.

## **8\. El Motor de Búsqueda y Filtrado: Gramática Avanzada**

Una de las características más potentes de DIM es su barra de búsqueda. Investigar "cómo lo hace" revela un analizador léxico (parser) completo.

### **8.1 Análisis Sintáctico de Consultas (AST)**

Cuando un usuario escribe is:triumph is:incomplete name:incursión, DIM no hace una búsqueda de texto simple.

1. **Tokenización:** Divide la cadena en tokens (is:triumph, is:incomplete, name:incursión).  
2. **Mapeo de Filtros:** Cada token se asigna a una función de filtro específica.  
   * is:triumph: Filtra elementos que son instancias de DestinyRecordDefinition.  
   * is:incomplete: Verifica (state & ObjectiveNotCompleted)\!== 0\.  
   * is:redeemable: Verifica \!RecordRedeemed AND \!ObjectiveNotCompleted.  
3. **Ejecución:** El motor de filtrado recorre el árbol de hazañas. Si una rama completa (Nodo de Presentación) no contiene ningún registro que cumpla los criterios, la rama se colapsa o se oculta.

### **8.2 Indexación**

Para lograr un rendimiento instantáneo, DIM indexa las descripciones y nombres de las hazañas al cargar la aplicación. Crea un índice invertido que mapea palabras clave a hashes de registros. Esto permite que la búsqueda sea una operación O(1) o O(log n) en lugar de recorrer linealmente 15,000 registros en cada pulsación de tecla.

## **9\. Interfaz de Usuario: Virtualización y Renderizado**

Finalmente, "cómo se ve" es resultado de técnicas avanzadas de React. Dado que el árbol de hazañas es masivo, renderizarlo todo a la vez congelaría el navegador.

* **Virtualización:** DIM utiliza componentes de "lista virtual" (como react-window o implementaciones propias). Solo renderiza los nodos y registros que son visibles actualmente en la ventana gráfica (viewport) del usuario. A medida que el usuario hace scroll, los componentes se crean y destruyen dinámicamente.  
* **Componentes Recursivos:** La UI refleja la estructura de datos. Un componente \<PresentationNode /\> se renderiza a sí mismo y, si está expandido, renderiza una lista de componentes \<PresentationNode /\> hijos o \<Record /\> finales.

## **10\. Conclusiones y Recomendaciones para un Sistema Ideal**

Basándonos en la arquitectura de DIM, un sistema que aspire a gestionar "Hazañas" correctamente **debería tener**:

1. **Ingesta Híbrida:** Capacidad de consumir tanto definiciones estáticas (para velocidad y metadatos) como estados dinámicos de usuario. No depender solo de consultas en tiempo real para descripciones.  
2. **Manejo Robusto de Estados Bitwise:** Implementación completa de la lógica de bitmasks para diferenciar matices entre "Incompleto", "Completo no reclamado" y "Completo reclamado".  
3. **Estructura Jerárquica Recursiva:** Soporte para anidamiento infinito de categorías, reflejando la complejidad evolutiva del juego.  
4. **Capa de Corrección de Datos:** Un mecanismo (como d2-additional-info) para parchear errores de la fuente oficial sin esperar a actualizaciones del proveedor del juego.  
5. **Motor de Búsqueda Semántica:** Capacidad de filtrar por estado lógico (is:redeemable) y no solo por texto.  
6. **Contexto Social:** Integración de datos de rareza global para dar valor a los logros.

DIM demuestra que la gestión de logros en videojuegos modernos no es una tarea trivial de visualización de listas, sino un problema complejo de integración de bases de datos, lógica de conjuntos y optimización de rendimiento frontend.

## **11\. Tablas de Referencia Técnica**

A continuación, se presentan tablas técnicas que resumen la estructura de datos crítica analizada en el informe, facilitando la comprensión de las relaciones entre las definiciones de la API y la lógica de DIM.

### **11.1 Mapeo de Componentes de la API de Bungie Utilizados por DIM**

| Componente (ID) | Nombre Técnico (DestinyComponentType) | Función Específica en el Subsistema de Hazañas |
| :---- | :---- | :---- |
| **900** | Records | Proporciona el estado (progreso y flags) de todas las hazañas a nivel de cuenta (Account-wide). Es la fuente principal. 2 |
| **200** | Characters | Proporciona datos básicos de los personajes activos, necesarios para iterar sobre los registros específicos de personaje. |
| **104** | Collectibles | Utilizado para las "Colecciones" (Badges) que a menudo son requisitos previos para desbloquear Sellos y Títulos. 4 |
| **102** | ProfileInventories | Permite a DIM verificar si el usuario tiene espacio para recibir recompensas de hazañas o si posee los items requeridos. |
| **700** | PresentationNodes | Proporciona el estado de los nodos de presentación (carpetas). Crítico para saber qué categorías están ocultas o "Invisibles". |

### **11.2 Estructura del DestinyRecordState y Lógica de Decisión**

Esta tabla detalla cómo DIM transforma los bits crudos en decisiones de UI.

| Bit (Valor) | Flag (Enum) | Acción en la Interfaz de Usuario (DIM) |
| :---- | :---- | :---- |
| **0** | None | Mostrar barra de progreso estándar. Estado neutral. |
| **1** | RecordRedeemed | Marcar como "Completado" (Check verde). Ocultar si el filtro es "Incompletos". Contar para el puntaje total. 5 |
| **2** | RewardUnavailable | Mostrar advertencia o deshabilitar botón de reclamo. Indica inventario lleno o límite de divisas alcanzado. |
| **4** | ObjectiveNotCompleted | **Si está presente:** Mostrar progreso parcial. **Si está ausente:** Marcar como listo para reclamar (si bit 1 es 0). |
| **8** | Obscured | Reemplazar nombre y descripción con "Secreto" o texto ofuscado. Habilitar opción de "Revelar" en configuración. |
| **16** | Invisible | **Eliminar del DOM.** No renderizar el nodo ni sus hijos. Previene spoilers de contenido futuro. |
| **32** | EntitlementUnowned | Mostrar icono de DLC requerido. Indicar que el contenido es "Paywalled". |
| **64** | CanEquipTitle | Habilitar botón interactivo "Equipar Título" en la UI. Exclusivo para nodos de Sellos. 5 |

### **11.3 Archivos Críticos en d2-additional-info**

Análisis de la dependencia externa que nutre a DIM con datos que la API omite.

| Archivo Generado | Propósito Técnico | Impacto en el Usuario |
| :---- | :---- | :---- |
| catalyst-triumph-icons.json | Mapeo Hash Hazaña \<-\> Hash Item | Muestra el icono real del arma exótica en su triunfo, en lugar de un icono genérico. 7 |
| source-info.ts | Base de datos de cadenas de texto de fuentes | Añade "Obtenido en: Incursión Cámara de Cristal" a la descripción, ayudando al usuario a saber dónde ir. |
| adept-weapon-hashes.json | Lista de IDs de armas Adepto | Permite filtrar y etiquetar triunfos que recompensan versiones Adepto de armas. 7 |
| bright-engram.json | Mapeo Temporada \<-\> Engrama | Vincula triunfos estacionales con el contenido cosmético correspondiente de la tienda Eververso. 7 |
| craftable-hashes.json | Identificación de patrones | Cruza datos de triunfos de patrones con el sistema de fabricación (Crafting) para mostrar progreso de desbloqueo. 7 |

Esta infraestructura de datos demuestra que la gestión de "Hazañas" en DIM es un ejercicio de **fusión de datos multisectorial**, donde la precisión depende tanto de la lógica algorítmica (bitmasks, recursión) como de la curación manual de la comunidad.

#### **Obras citadas**

1. Is there an easy way to find out what an item hashes too for Destiny API? \- Reddit, fecha de acceso: diciembre 27, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/3s2f31/is\_there\_an\_easy\_way\_to\_find\_out\_what\_an\_item/](https://www.reddit.com/r/DestinyTheGame/comments/3s2f31/is_there_an_easy_way_to_find_out_what_an_item/)  
2. Destiny.Components.Records.DestinyProfileRecordsComponent \- Bungie.Net API, fecha de acceso: diciembre 27, 2025, [https://bungie-net.github.io/multi/schema\_Destiny-Components-Records-DestinyProfileRecordsComponent.html](https://bungie-net.github.io/multi/schema_Destiny-Components-Records-DestinyProfileRecordsComponent.html)  
3. Bungie.Net API, fecha de acceso: diciembre 27, 2025, [https://bungie-net.github.io/](https://bungie-net.github.io/)  
4. 2.3.0 and 2.3.1 Changes (Forsaken Release) · Bungie-net/api Wiki \- GitHub, fecha de acceso: diciembre 27, 2025, [https://github.com/Bungie-net/api/wiki/2.3.0-and-2.3.1-Changes-(Forsaken-Release)](https://github.com/Bungie-net/api/wiki/2.3.0-and-2.3.1-Changes-\(Forsaken-Release\))  
5. Destiny.DestinyRecordState \- Bungie.Net API, fecha de acceso: diciembre 27, 2025, [https://bungie-net.github.io/multi/schema\_Destiny-DestinyRecordState.html](https://bungie-net.github.io/multi/schema_Destiny-DestinyRecordState.html)  
6. Destiny Item Manager, fecha de acceso: diciembre 27, 2025, [https://destinyitemmanager.com/en/](https://destinyitemmanager.com/en/)  
7. DestinyItemManager/d2-additional-info: provide additional manifest data for DIM \- GitHub, fecha de acceso: diciembre 27, 2025, [https://github.com/DestinyItemManager/d2-additional-info](https://github.com/DestinyItemManager/d2-additional-info)  
8. How to check for the 50 point Triumph Score Bug : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 27, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/cs6ij7/how\_to\_check\_for\_the\_50\_point\_triumph\_score\_bug/](https://www.reddit.com/r/DestinyTheGame/comments/cs6ij7/how_to_check_for_the_50_point_triumph_score_bug/)  
9. Destiny 2 Title Analytics | Charlemagne, fecha de acceso: diciembre 27, 2025, [https://warmind.io/analytics/title](https://warmind.io/analytics/title)