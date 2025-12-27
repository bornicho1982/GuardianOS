# **Arquitectura de Sistemas y Desarrollo de Aplicaciones de Escritorio de Alto Rendimiento para Destiny 2 en C\#: Migración de Lógica Web, Algoritmos de Optimización y Adaptación a "Edge of Fate"**

## **1\. Resumen Ejecutivo y Visión Arquitectónica**

El presente informe técnico detalla la investigación, diseño y arquitectura de software necesaria para el desarrollo de una aplicación de escritorio nativa orientada a la gestión de inventario y optimización de equipamiento (*loadouts*) para el videojuego *Destiny 2*. Este análisis surge como respuesta a las limitaciones inherentes de las arquitecturas web actuales —representadas por herramientas líderes como *Destiny Item Manager* (DIM) y *D2ArmorPicker*— ante la creciente complejidad mecánica introducida por Bungie, específicamente con la llegada de la expansión *Edge of Fate* y el sistema *Armor 3.0*.

La propuesta se fundamenta en la migración de la lógica de negocio, actualmente residente en entornos JavaScript/TypeScript (React/Redux), hacia un ecosistema de alto rendimiento basado en **.NET 8/9 (C\#)**. El objetivo primordial es capitalizar las ventajas del código gestionado de bajo nivel: manejo estricto de memoria, paralelismo de datos (SIMD) para cálculos combinatorios y acceso directo al sistema de archivos para bases de datos relacionales locales (SQLite).

El documento aborda cuatro pilares críticos:

1. **Infraestructura de Datos:** Implementación de clientes API robustos con autenticación OAuth 2.0 PKCE y manejo eficiente del "Manifiesto" de Destiny 2 mediante SQLite.  
2. **Migración de Lógica Web:** Transposición de patrones de estado Redux hacia arquitecturas reactivas MVVM (Model-View-ViewModel) utilizando ReactiveUI y AvaloniaUI para interfaces multiplataforma de alto rendimiento.  
3. **Motor de Optimización Matemática:** Reemplazo de algoritmos heurísticos web por solvers de Programación Lineal Entera (MIP) y Programación de Restricciones (CP-SAT) mediante Google OR-Tools, adaptados para resolver la nueva complejidad de *Armor 3.0* (escalas lineales 0-200 y slots de ajuste dinámico).  
4. **Inteligencia Artificial Integrada:** Incorporación de *Microsoft Semantic Kernel* para habilitar capacidades de RAG (Retrieval-Augmented Generation) sobre la base de conocimientos de perks y mods.

## ---

**2\. Fundamentos Tecnológicos y Justificación de la Plataforma**

El ecosistema actual de herramientas de terceros para *Destiny 2* está dominado por Aplicaciones Web Progresivas (PWA).1 Si bien estas ofrecen accesibilidad universal, enfrentan cuellos de botella significativos cuando se trata de procesamiento intensivo de datos y gestión de memoria a gran escala.

### **2.1 Limitaciones de la Arquitectura Web (SPA/PWA)**

Las herramientas existentes como DIM operan bajo las restricciones del motor V8 (Chrome/Edge) o SpiderMonkey (Firefox).

* **Gestión de Memoria:** El manejo de miles de objetos de inventario (cada uno con cientos de propiedades derivadas del manifiesto) genera una presión considerable sobre el Garbage Collector de JavaScript, provocando micro-pausas en la interfaz durante operaciones de filtrado masivo.3  
* **Monohilo:** Aunque los *Service Workers* permiten cierto trabajo en segundo plano, la lógica principal de la UI y el cálculo de loadouts a menudo compiten por el hilo principal, degradando la experiencia de usuario en dispositivos de gama media.  
* **Persistencia:** El almacenamiento depende de localStorage o IndexedDB, que son asíncronos y tienen cuotas de almacenamiento variables según el navegador, complicando el almacenamiento de bases de datos relacionales complejas como el historial de partidas o el análisis estadístico profundo.

### **2.2 Ventajas de la Arquitectura Nativa.NET (C\#)**

La elección de C\# sobre tecnologías web o Electron se justifica por métricas de rendimiento y capacidades arquitectónicas:

* **Sistema de Tipos y Estructuras de Memoria:** *Destiny 2* posee más de 30.000 definiciones de ítems. C\# permite el uso de Structs y Records inmutables para representar estos datos, minimizando la sobrecarga de memoria (overhead) y mejorando la localidad de caché en la CPU, algo imposible de controlar en JavaScript.  
* **SIMD y Vectorización:** Para el motor de optimización de armaduras,.NET ofrece intrínsecos de hardware (System.Runtime.Intrinsics) que permiten procesar múltiples combinaciones de estadísticas en un solo ciclo de reloj, acelerando exponencialmente la resolución de problemas combinatorios NP-duros como el de la mochila multidimensional.4  
* **Acceso Directo a SQLite:** La capacidad de interactuar con el archivo de Manifiesto de Bungie (mobile\_world\_content) directamente mediante punteros nativos o micro-ORMs como Dapper elimina la necesidad de serialización/deserialización JSON constante, reduciendo los tiempos de arranque y consulta.6

## ---

**3\. Integración Profunda con la API de Bungie**

El núcleo de la aplicación reside en su capacidad para comunicarse de manera segura y eficiente con la *Bungie.net Platform*. A diferencia de un entorno web, una aplicación de escritorio requiere un enfoque diferente para la autenticación y el manejo de redes.

### **3.1 Protocolo de Autenticación: OAuth 2.0 con PKCE**

La seguridad es primordial. Las aplicaciones de escritorio se consideran "clientes públicos" en la terminología OAuth, lo que significa que no pueden mantener secretos de cliente de forma segura. Por ello, el uso del flujo *Authorization Code with Proof Key for Code Exchange* (PKCE) es obligatorio según las mejores prácticas del IETF y Bungie.8

#### **Implementación Técnica del Flujo PKCE**

El ciclo de vida de la autenticación en C\# debe implementarse siguiendo estos pasos rigurosos:

1. Generación de Claves Efímeras:  
   Se debe generar un code\_verifier (cadena aleatoria de alta entropía) y derivar un code\_challenge mediante SHA-256.  
   C\#  
   // Ejemplo conceptual de generación PKCE en C\#  
   using var sha256 \= SHA256.Create();  
   var challengeBytes \= sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier));  
   var codeChallenge \= Base64UrlEncoder.Encode(challengeBytes);

2. Listener de Redirección Local:  
   A diferencia de las aplicaciones web que redirigen a una URL de callback en el servidor, la aplicación de escritorio debe levantar temporalmente un servidor HTTP local. Se recomienda el uso de System.Net.HttpListener escuchando en http://localhost:{puerto}/.10  
   * *Consideración Crítica:* El listener debe estar activo solo durante la ventana de autenticación para minimizar la superficie de ataque. Una vez recibido el código de autorización, el listener debe cerrarse inmediatamente.  
3. Intercambio y Persistencia Segura:  
   El intercambio del código por tokens (Access y Refresh) se realiza mediante una petición POST al endpoint /Platform/App/OAuth/Token/.11  
   * **Almacenamiento:** Bajo ninguna circunstancia se deben guardar los tokens en archivos de texto plano (XML/JSON). Se debe utilizar la API de Protección de Datos de Windows (DataProtectionScope.CurrentUser) o el *Credential Locker* del sistema operativo para cifrar los tokens en reposo.

### **3.2 Arquitectura del Cliente API: DotNetBungieAPI**

Para la capa de comunicación, el análisis comparativo identifica a **DotNetBungieAPI** como la solución más robusta frente a alternativas como *GhostSharper* o implementaciones manuales.12

#### **Análisis Comparativo de Wrappers**

| Característica | DotNetBungieAPI | GhostSharper | Cliente HTTP Manual |
| :---- | :---- | :---- | :---- |
| **Mantenimiento** | Activo, orientado a DI | Generado auto., mantenimiento bajo | Alto costo de mantenimiento |
| **Manejo de Manifiesto** | Automático, repositorio integrado | Solo definiciones de clases | Requiere implementación completa |
| **Tipado** | Fuerte (DefinitionHashPointer\<T\>) | Fuerte (Modelos POCO) | Débil o manual |
| **Inyección de Dependencias** | Nativa (IServiceCollection) | No nativa | Manual |

Estrategia de Implementación:  
Se debe registrar DotNetBungieAPI como un servicio Singleton dentro del contenedor de inyección de dependencias de la aplicación (usando Microsoft.Extensions.DependencyInjection). Esto garantiza una gestión centralizada de la configuración del cliente, las claves API y el ciclo de vida de las conexiones HTTP.

* **Manejo de Rate Limiting:** La librería debe configurarse para respetar los encabezados X-Throttle-Seconds devueltos por Bungie, implementando un patrón *Token Bucket* o *Leaky Bucket* local para encolar peticiones y evitar bloqueos temporales de la API, una robustez que scripts simples de Python a menudo ignoran.15

### **3.3 Gestión Avanzada del Manifiesto (SQLite)**

El Manifiesto de *Destiny 2* es una base de datos SQLite comprimida que contiene la "verdad estática" del juego. Su manejo eficiente es vital para el rendimiento de la aplicación.6

#### **Pipeline de Sincronización**

1. **Verificación de Versión:** Al inicio, la aplicación consulta Destiny2.GetDestinyManifest.  
2. **Descarga Diferencial:** Si la versión remota difiere de la local, se descarga el archivo .content (aprox. 100MB comprimido).  
3. **Extracción y Conexión:** Se descomprime el archivo ZIP en memoria o disco y se establece una conexión de solo lectura.

#### **Capa de Acceso a Datos (DAL)**

Para maximizar el rendimiento de lectura, se desaconseja el uso de ORMs pesados como Entity Framework Core para el manifiesto. En su lugar, se debe utilizar **SQLite-net-pcl** o **Dapper**.12

* **Patrón de Acceso:** Dado que las tablas del manifiesto almacenan los datos como BLOBs JSON indexados por un ID (hash), la aplicación debe implementar un deserializador de alto rendimiento (usando System.Text.Json con *Source Generators*) para convertir estos BLOBs en objetos C\# bajo demanda.  
* **Caché de Segundo Nivel:** Para evitar deserializaciones repetitivas de ítems comunes (como "Gjallarhorn" o armaduras populares), se debe implementar una caché en memoria (IMemoryCache) con políticas de desalojo LRU (Least Recently Used), optimizando drásticamente el renderizado de la interfaz de inventario.

## ---

**4\. Migración de Lógica de Negocio: De SPA a Escritorio**

La transición de una *Single Page Application* (SPA) basada en React/Redux a una aplicación de escritorio.NET implica una reingeniería fundamental de los patrones de gestión de estado y flujo de datos.

### **4.1 De Redux a ReactiveUI y MVVM**

El patrón Redux utilizado por DIM mantiene un único árbol de estado inmutable (store) que se actualiza mediante acciones y reductores puros.3 En el mundo.NET, el equivalente arquitectónico más potente para interfaces ricas es el patrón **Model-View-ViewModel (MVVM)** potenciado por Programación Reactiva Funcional (FRP).17

#### **Mapeo de Conceptos Arquitectónicos**

| Concepto Redux (DIM) | Concepto ReactiveUI (C\#) | Implementación Técnica |
| :---- | :---- | :---- |
| **Store (Global State)** | **ReactiveObject / Services** | Servicios inyectados que exponen propiedades IObservable\<T\>. |
| **Selector (reselect)** | **WhenAnyValue / OAPH** | ObservableAsPropertyHelper\<T\> que computa valores derivados automáticamente. |
| **Action / Dispatch** | **ReactiveCommand** | Comandos asíncronos que encapsulan lógica de ejecución y estado de "ocupado". |
| **Reducer** | **Scan / Merge** | Operadores LINQ reactivos que acumulan cambios de estado sobre flujos de eventos. |

Caso de Estudio: Filtrado de Inventario  
En DIM, un selector toma la lista completa de ítems y la cadena de búsqueda, devolviendo una nueva lista filtrada cada vez que algo cambia.  
En C\#, utilizando DynamicData (una librería compañera de ReactiveUI), esto se modela como un flujo de datos continuo:

C\#

// Lógica reactiva en C\# para filtrado de alto rendimiento  
\_sourceCache.Connect()  
   .Filter(searchQueryObservable) // Se reevalúa solo cuando cambia la query  
   .Sort(sortComparer)  
   .Bind(out \_visibleItems) // Actualiza la colección de la UI de forma thread-safe  
   .Subscribe();

Este enfoque es superior en rendimiento porque DynamicData solo procesa los deltas (cambios) en la colección, en lugar de regenerar toda la lista como lo hacen muchos selectores de Redux mal optimizados.

### **4.2 Lógica de Dominio: Estructura del Inventario**

La estructura de datos interna debe reflejar la complejidad del juego pero optimizada para acceso rápido. Se propone un modelo de **Entidad-Componente** simplificado en memoria.19

* **Clase DestinyItem:** No debe ser un mero contenedor de datos (POCO). Debe incluir métodos de dominio para evaluar reglas de negocio (e.g., CanBeEquippedBy(Character c)).  
* **Buckets y Ubicaciones:** La gestión de dónde está un ítem (Personaje vs. Depósito) debe manejarse mediante un sistema de indexación dual: un diccionario global Dictionary\<ulong, DestinyItem\> para búsqueda por ID de instancia, y colecciones agrupadas por BucketHash para renderizado de UI. Esto permite operaciones O(1) para movimientos y actualizaciones.

### **4.3 Migración del Motor de Búsqueda (Lexer/Parser)**

El sistema de búsqueda de DIM (ej. is:weapon stat:total:\>60) es una de sus características más potentes.2 Replicar esto en C\# requiere construir un compilador de consultas:

1. **Lexer:** Tokeniza la entrada del usuario en componentes (Filtro is, Argumento weapon, Operador \>).  
2. **Parser:** Construye un Árbol de Expresión Abstracta (AST).  
3. Compilador LINQ: Transforma el AST en un delegado Func\<DestinyItem, bool\> compilado en tiempo de ejecución utilizando Árboles de Expresión (System.Linq.Expressions).  
   Este enfoque permite que las búsquedas sean evaluadas a velocidad nativa, filtrando miles de ítems en microsegundos, superando significativamente a las implementaciones de intérpretes de filtros en JavaScript.

## ---

**5\. Diseño de Interfaz de Usuario con AvaloniaUI**

Para la capa de presentación, se selecciona **AvaloniaUI** sobre WPF o WinUI debido a su arquitectura de renderizado moderna basada en Skia (independiente de la GPU del sistema operativo) y su capacidad multiplataforma real (Windows, macOS, Linux).21

### **5.1 Virtualización de Listas y Rendimiento Gráfico**

Un inventario completo puede contener hasta 600 ítems. Renderizar 600 controles de usuario complejos (con iconos, barras de estadísticas, bordes de masterwork) simultáneamente colapsaría cualquier framework de UI ingenuo.

* **Virtualización:** Se debe implementar ItemsRepeater o ListBox con VirtualizingStackPanel. Avalonia recicla los contenedores visuales que salen de la pantalla, manteniendo el conteo de objetos visuales bajo y constante, independientemente del tamaño del inventario.23  
* **Composición de Imagen:** Los iconos de *Destiny 2* se componen de múltiples capas (fondo, imagen del ítem, overlay de temporada, borde de rareza, icono de elemento). En lugar de usar múltiples controles Image apilados (que consumen mucha memoria), se recomienda utilizar SkiaSharp para dibujar estas capas en un único mapa de bits (bitmap) en memoria o utilizar un *Shader* personalizado para componerlas en la GPU.

### **5.2 Arquitectura de Navegación y Estructura**

La aplicación debe seguir una estructura de navegación basada en **View-First** o **ViewModel-First**, donde el ContentControl principal de la ventana se enlaza a una propiedad CurrentViewModel.

* **Inyección de Dependencias en Vistas:** Utilizando librerías como Splat o el contenedor nativo de.NET, las vistas se resuelven automáticamente según el ViewModel activo.25  
* **Panel de "Smart Moves":** La funcionalidad de mover ítems (arrastrar y soltar desde el depósito al personaje) debe implementarse visualmente como operaciones de Drag & Drop que disparan comandos asíncronos en el backend. La UI debe mostrar estados de "carga" optimistas (actualizar la UI antes de que la API responda) para una sensación de fluidez, revertiendo el cambio solo si la API falla.

## ---

**6\. El Motor de Optimización "Loadout Optimizer": Algoritmos y Matemáticas**

La funcionalidad más crítica para los jugadores avanzados es la optimización de armaduras (*Armor Picker*). Este problema consiste en seleccionar una combinación de 4 piezas de armadura (Casco, Guanteletes, Pecho, Piernas) y un objeto de clase, más una configuración de mods, tal que se maximicen las estadísticas deseadas bajo ciertas restricciones (costo de energía, exóticos permitidos).26

### **6.1 Formulación Matemática: Problema de la Mochila Multidimensional**

El problema se modela formalmente como una variación del **Problema de la Mochila Multidimensional (d-KP)** o un Problema de Satisfacción de Restricciones (CSP).4

Variables de Decisión:  
Sea $x\_{i,j}$ una variable binaria donde $x\_{i,j} \= 1$ si el ítem $i$ es seleccionado para el slot $j$ (donde $j \\in \\{Helmet, Arms, Chest, Legs, ClassItem\\}$), y 0 en caso contrario.  
Función Objetivo:  
Maximizar $Z \= \\sum\_{s \\in Stats} W\_s \\cdot V\_s$, donde $W\_s$ es el peso/prioridad del stat $s$ asignado por el usuario y $V\_s$ es el valor total final de ese stat.  
**Restricciones Clásicas:**

1. **Exclusividad de Slot:** $\\sum\_{i \\in Inventario\_j} x\_{i,j} \= 1 \\quad \\forall j$.  
2. **Restricción Exótica:** $\\sum\_{j} \\sum\_{i \\in Inventario\_j} IsExotic(i) \\cdot x\_{i,j} \\le 1$.  
3. **Límite de Mods:** La suma del costo de energía de los mods necesarios para alcanzar los stats deseados no puede exceder la capacidad de energía de la armadura (generalmente 10, o 11 en armaduras Artífice/Tier 4+).29

### **6.2 Implementación con Google OR-Tools (CP-SAT)**

Mientras que las herramientas web como D2ArmorPicker utilizan algoritmos personalizados de Branch and Bound en JavaScript 26, en C\# podemos integrar Google OR-Tools, una suite de optimización de operaciones de clase mundial.5  
El solver CP-SAT (Constraint Programming \- SATisfiability) es ideal para este dominio discreto.  
**Pseudocódigo de Implementación en C\#:**

C\#

using Google.OrTools.Sat;

public LoadoutSolution Optimize(List\<Armor\> armors, OptimizationGoal goal) {  
    CpModel model \= new CpModel();  
    var itemVars \= new Dictionary\<Armor, BoolVar\>();

    // 1\. Crear variables booleanas para cada pieza de armadura disponible  
    foreach(var armor in armors) {  
        itemVars\[armor\] \= model.NewBoolVar(armor.Name);  
    }

    // 2\. Restricción: Seleccionar exactamente uno por slot  
    foreach(var slot in Slots) {  
        var itemsInSlot \= armors.Where(a \=\> a.Slot \== slot);  
        model.Add(LinearExpr.Sum(itemsInSlot.Select(i \=\> itemVars\[i\])) \== 1);  
    }

    // 3\. Restricción: Máximo un exótico  
    var exotics \= armors.Where(a \=\> a.IsExotic);  
    model.Add(LinearExpr.Sum(exotics.Select(i \=\> itemVars\[i\])) \<= 1);

    // 4\. Definición de Stats Totales (Expresión Lineal)  
    var totalStats \= new Dictionary\<StatHash, LinearExpr\>();  
    foreach(var stat in StatHashes) {  
        totalStats\[stat\] \= LinearExpr.Sum(armors.Select(i \=\> itemVars\[i\] \* i.Stats\[stat\]));  
    }  
      
    //... Agregar lógica de mods y restricciones de usuario...

    // 5\. Función Objetivo (Maximizar stats priorizados)  
    model.Maximize(LinearExpr.Sum(goal.Priorities.Select(p \=\> totalStats \* p.Weight)));

    // 6\. Resolver  
    CpSolver solver \= new CpSolver();  
    var status \= solver.Solve(model);  
    //... Procesar resultado...  
}

Este enfoque permite resolver combinaciones complejas en milisegundos, aprovechando la capacidad multihilo del solver CP-SAT, algo que es extremadamente difícil de lograr en un entorno de navegador monohilo.

### **6.3 Adaptación a "Armor 3.0" y "Edge of Fate"**

La expansión *Edge of Fate* introduce cambios radicales que deben ser modelados en el optimizador. Las versiones anteriores de los optimizadores asumían "Tiers" (rangos de 10 puntos). El nuevo sistema es lineal (cada punto cuenta hasta 200\) e introduce **Tuning Slots** en armaduras Tier 5\.31

#### **Nuevas Variables para Tier 5 (Tuning Slots)**

En Armor 3.0, una pieza de armadura Tier 5 tiene un "Tuning Slot" que permite añadir \+5 a un stat específico o distribuir puntos. Esto significa que los stats de un ítem ya no son constantes, sino que dependen de una variable de decisión adicional.

* **Modelado:** Para cada ítem Tier 5 seleccionado ($x\_{i,j}=1$), se introduce una variable entera $t\_{i, stat} \\in \\{0, 5\\}$ que representa la asignación del Tuning Slot.  
* **Restricción:** $\\sum\_{stat} t\_{i, stat} \\le 5$ (o la regla específica que Bungie imponga, por ejemplo, solo un stat puede recibir el bono).

Esto transforma el problema en uno más complejo, donde el solver no solo elige la armadura, sino también cómo *configurarla*. OR-Tools es excepcionalmente capaz de manejar estas variables condicionales sin explosión combinatoria, a diferencia de los algoritmos de fuerza bruta o heurísticos simples de JavaScript.

#### **Lógica "Zero-Waste" (Cero Desperdicio) en un Sistema Lineal**

En el sistema anterior, tener 79 puntos en un stat era un "desperdicio" porque valía lo mismo que 70\. En el nuevo sistema lineal (0-200), 79 es mejor que 78.31  
Sin embargo, el concepto de "Zero-Waste" evoluciona: ahora se trata de Eficiencia de Mods. Los usuarios querrán minimizar el uso de mods de armadura costosos para alcanzar sus objetivos.

* **Nueva Función Objetivo:** Maximizar Stats Totales \- (Penalización \* Costo de Energía de Mods Usados).  
* Esto prioriza construcciones que alcanzan los stats deseados (ej. 100 Health) utilizando solo las stats base de la armadura, dejando la energía libre para mods de funcionalidad (cargadores, dex, etc.).

## ---

**7\. Inteligencia Artificial y Semantic Kernel: El Futuro del Buildcrafting**

Para diferenciar la aplicación de escritorio de las soluciones web, se propone la integración de **Microsoft Semantic Kernel (SK)** para ofrecer asistencia de IA generativa contextualizada.33

### **7.1 Arquitectura RAG (Retrieval-Augmented Generation) para Perks**

Los jugadores a menudo preguntan en lenguaje natural: "Quiero una build que regenere granadas rápido y me haga tanque". Las búsquedas tradicionales por palabras clave son limitadas.  
Mediante SK y un modelo de embeddings local (o conectado a Azure OpenAI), se puede implementar un sistema RAG:

1. **Ingesta:** Indexar todas las descripciones de perks, mods y fragmentos del Manifiesto en un almacén vectorial (Vector Store) local o en memoria (ej. *Qdrant* o implementaciones simples en memoria de SK).35  
2. **Búsqueda Semántica:** Cuando el usuario pide "tanque", el sistema busca vectores semánticamente cercanos como "Resistencia al daño", "Overshield", "Curación", "Health".36  
3. **Planificador (Stepwise Planner):** El *Stepwise Planner* de SK puede descomponer la solicitud del usuario en pasos ejecutables 37:  
   * *Paso 1:* Buscar armas con perks tipo "Demolitionist" (regenerar granada).  
   * *Paso 2:* Buscar armaduras con alto stat "Health" y "Grenade" (usando el nuevo mapeo de stats de Armor 3.0).29  
   * *Paso 3:* Configurar el optimizador (OR-Tools) con estas restricciones y ejecutarlo.

### **7.2 Interfaz de Chat Copilot**

Se puede integrar un componente de chat en AvaloniaUI donde el usuario interactúa con el "Ghost" (IA). El copilot no solo responde texto, sino que puede ejecutar acciones reales en la aplicación (ej. "Aplica este loadout", "Mueve estas armas") mediante *Plugins* de SK que envuelven los servicios de lógica de negocio en C\#.39

## ---

**8\. Análisis de Impacto de "Edge of Fate" (Armor 3.0) en la Arquitectura**

La expansión *Edge of Fate* representa el cambio más drástico en el sistema de RPG de *Destiny 2* en años. La arquitectura de la aplicación debe ser resiliente a estos cambios.

### **8.1 Mapeo Dinámico de Estadísticas**

Los seis stats tradicionales (Mobility, Resilience, etc.) desaparecen o se transforman en **Weapons, Health, Class, Grenade, Super, Melee**.29

* **Requerimiento Arquitectónico:** La base de datos y los modelos de objetos (POCOs) **no deben** tener propiedades hardcodeadas como item.Mobility. Deben usar un diccionario Dictionary\<uint, StatValue\> donde la clave es el Hash del stat del Manifiesto.  
* **UI Dinámica:** Las etiquetas de la interfaz deben enlazarse a las definiciones del Manifiesto. Si Bungie cambia el nombre de "Health" a "Vitality" en el futuro, la aplicación debe reflejarlo automáticamente sin recompilación, simplemente actualizando la base de datos SQLite local.

### **8.2 Estructura de "Armor Archetypes"**

Las armaduras ahora tendrán arquetipos fijos (ej. *Brawler*: Primario Melee, Secundario Health).41

* **Lógica de Filtrado:** El motor de búsqueda debe permitir filtrar por arquetipo (is:brawler). Esto requiere cruzar datos: el ítem tiene un stat de arquetipo o un perk intrínseco que define su arquetipo. La aplicación debe parsear estos perks ocultos para etiquetar correctamente los ítems en la UI.

## ---

**9\. Conclusión y Recomendaciones Finales**

La investigación concluye que el desarrollo de una aplicación de escritorio para *Destiny 2* en C\# es no solo viable, sino tecnológicamente superior a las alternativas web actuales para los escenarios de uso más exigentes introducidos por *Armor 3.0*.

**Recomendaciones Clave para el Desarrollo:**

1. **Adopción de Stack:** Utilizar **.NET 9**, **AvaloniaUI** y **ReactiveUI**. Esta combinación ofrece el mejor equilibrio entre rendimiento nativo, desarrollo multiplataforma y gestión de estado reactiva moderna.  
2. **Núcleo de Optimización:** Invertir fuertemente en la implementación de **Google OR-Tools**. La complejidad matemática de los *Tuning Slots* y la escala lineal 0-200 hacen que los algoritmos de fuerza bruta sean obsoletos. Un solver CP-SAT bien configurado será el diferencial competitivo clave.  
3. **Seguridad y Confianza:** Implementar **OAuth PKCE** de manera estricta y transparente. La confianza del usuario es frágil; el almacenamiento inseguro de credenciales puede destruir la reputación de la herramienta antes de su lanzamiento.  
4. **Diseño "Data-Driven":** Abstraer completamente la definición de stats y reglas de equipamiento basándose únicamente en el Manifiesto de Bungie. Evitar la lógica "hardcoded" para garantizar que la aplicación sobreviva a los cambios masivos de *Edge of Fate* sin necesidad de reescrituras mayores.

Esta arquitectura posiciona a la herramienta no solo como un gestor de inventario, sino como una plataforma de análisis y optimización de grado profesional, lista para la próxima era de *Destiny 2*.

## ---

**Apéndice A: Tabla Comparativa de Mapeo de Stats (Armor 2.0 vs Armor 3.0)**

Esta tabla ilustra la transformación de datos necesaria en la capa de migración de la base de datos local al procesar ítems legacy tras la actualización *Edge of Fate*.

| Stat Legacy (Actual) | Nuevo Stat (Edge of Fate) | Descripción del Cambio Mecánico y Efecto \>100 |
| :---- | :---- | :---- |
| **Mobility** | **Weapons** | Ya no afecta solo la velocidad de movimiento. Ahora mejora manejo, recarga y daño a combatientes menores/mayores. \>100 otorga probabilidad de munición extra y daño a jefes. |
| **Resilience** | **Health** | Se centra puramente en supervivencia. Otorga resistencia al flinch y salud por orbes. \>100 mejora la recarga de escudos y otorga sobre-escudo pasivo. |
| **Recovery** | **Class** | Desvinculado de la regeneración de salud. Ahora gobierna exclusivamente la habilidad de clase. \>100 otorga sobre-escudo al activar la habilidad. |
| **Discipline** | **Grenade** | Similar al actual, pero con escalado lineal. \>100 aumenta directamente el *daño* de las granadas (0-65%). |
| **Intellect** | **Super** | Reducción de cooldown. \>100 aumenta significativamente el *daño* de la Super (0-45%), crítico para DPS. |
| **Strength** | **Melee** | Reducción de cooldown. \>100 aumenta el daño cuerpo a cuerpo (0-30%), incluyendo gujas y melees cargados. |

**Nota Técnica:** La conversión de valores numéricos será 1:1, pero las "Tier Breakpoints" (cada 10 puntos) desaparecen. La UI debe reflejar esto mostrando el beneficio exacto por punto (ej. "143 Weapons: \+14.3% Reload Speed") en lugar de bloques de niveles.

#### **Obras citadas**

1. Home · DestinyItemManager/DIM Wiki · GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/DestinyItemManager/DIM/wiki](https://github.com/DestinyItemManager/DIM/wiki)  
2. Destiny Item Manager, fecha de acceso: diciembre 26, 2025, [https://destinyitemmanager.com/en/](https://destinyitemmanager.com/en/)  
3. How to choose the Redux state shape for an app with list/detail views and pagination?, fecha de acceso: diciembre 26, 2025, [https://stackoverflow.com/questions/33940015/how-to-choose-the-redux-state-shape-for-an-app-with-list-detail-views-and-pagina](https://stackoverflow.com/questions/33940015/how-to-choose-the-redux-state-shape-for-an-app-with-list-detail-views-and-pagina)  
4. The Knapsack Problem | OR-Tools \- Google for Developers, fecha de acceso: diciembre 26, 2025, [https://developers.google.com/optimization/pack/knapsack](https://developers.google.com/optimization/pack/knapsack)  
5. Get Started with OR-Tools for .Net \- Google for Developers, fecha de acceso: diciembre 26, 2025, [https://developers.google.com/optimization/introduction/dotnet](https://developers.google.com/optimization/introduction/dotnet)  
6. Manifest | BungieNetPlatform, fecha de acceso: diciembre 26, 2025, [https://destinydevs.github.io/BungieNetPlatform/docs/Manifest](https://destinydevs.github.io/BungieNetPlatform/docs/Manifest)  
7. A Python Wrapper for Bungie's API. : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/oni4j1/a\_python\_wrapper\_for\_bungies\_api/](https://www.reddit.com/r/DestinyTheGame/comments/oni4j1/a_python_wrapper_for_bungies_api/)  
8. Implement OAuth 2.0 in Windows Apps \- Microsoft Learn, fecha de acceso: diciembre 26, 2025, [https://learn.microsoft.com/en-us/windows/apps/develop/security/oauth2](https://learn.microsoft.com/en-us/windows/apps/develop/security/oauth2)  
9. Desktop app that calls web APIs: Code configuration \- Microsoft Learn, fecha de acceso: diciembre 26, 2025, [https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration)  
10. Thread: \[RESOLVED\] OAuth2 and desktop app \- VBForums, fecha de acceso: diciembre 26, 2025, [https://www.vbforums.com/showthread.php?887758-RESOLVED-OAuth2-and-desktop-app](https://www.vbforums.com/showthread.php?887758-RESOLVED-OAuth2-and-desktop-app)  
11. Quick OAuth2 Integration in .NET with Bee.OAuth2 \- Medium, fecha de acceso: diciembre 26, 2025, [https://medium.com/@jeff377/quick-oauth2-integration-in-net-with-bee-oauth2-3823e01ba44c](https://medium.com/@jeff377/quick-oauth2-integration-in-net-with-bee-oauth2-3823e01ba44c)  
12. EndGameGl/DotNetBungieAPI: .NET Bungie.net API wrapper. \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/EndGameGl/DotNetBungieAPI](https://github.com/EndGameGl/DotNetBungieAPI)  
13. joshhunt/GhostSharper: C\# classes for the Bungie.net API \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/joshhunt/GhostSharper](https://github.com/joshhunt/GhostSharper)  
14. c\# Web Api \- generic wrapper class for all api responses \- Stack Overflow, fecha de acceso: diciembre 26, 2025, [https://stackoverflow.com/questions/47001587/c-sharp-web-api-generic-wrapper-class-for-all-api-responses](https://stackoverflow.com/questions/47001587/c-sharp-web-api-generic-wrapper-class-for-all-api-responses)  
15. Introducing BungIO \- a python bungie api wrapper : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/12lt3f1/introducing\_bungio\_a\_python\_bungie\_api\_wrapper/](https://www.reddit.com/r/DestinyTheGame/comments/12lt3f1/introducing_bungio_a_python_bungie_api_wrapper/)  
16. Obtaining Destiny Definitions "The Manifest" · Bungie-net/api Wiki \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/Bungie-net/api/wiki/Obtaining-Destiny-Definitions-%22The-Manifest%22](https://github.com/Bungie-net/api/wiki/Obtaining-Destiny-Definitions-%22The-Manifest%22)  
17. ReactiveUI | Avalonia Docs, fecha de acceso: diciembre 26, 2025, [https://docs.avaloniaui.net/docs/concepts/reactiveui/](https://docs.avaloniaui.net/docs/concepts/reactiveui/)  
18. A Simple Redux library for C\# developers, using Reactive Extensions | by David Bottiau, fecha de acceso: diciembre 26, 2025, [https://medium.com/@dbottiau/a-simple-redux-library-for-c-developers-using-reactive-extensions-453413a2b911](https://medium.com/@dbottiau/a-simple-redux-library-for-c-developers-using-reactive-extensions-453413a2b911)  
19. How can I implement an inventory that stores different types of items?, fecha de acceso: diciembre 26, 2025, [https://gamedev.stackexchange.com/questions/178665/how-can-i-implement-an-inventory-that-stores-different-types-of-items](https://gamedev.stackexchange.com/questions/178665/how-can-i-implement-an-inventory-that-stores-different-types-of-items)  
20. I updated my guide for DIM / Inventory Management / The Loadout Optimizer \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1dhseyn/i\_updated\_my\_guide\_for\_dim\_inventory\_management/](https://www.reddit.com/r/DestinyTheGame/comments/1dhseyn/i_updated_my_guide_for_dim_inventory_management/)  
21. Choosing Between WPF and Avalonia — Need Advice from Experienced Devs \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/dotnet/comments/1ol6f22/choosing\_between\_wpf\_and\_avalonia\_need\_advice/](https://www.reddit.com/r/dotnet/comments/1ol6f22/choosing_between_wpf_and_avalonia_need_advice/)  
22. WinUI vs WPF vs UWP \- Avalonia UI, fecha de acceso: diciembre 26, 2025, [https://avaloniaui.net/blog/winui-vs-wpf-vs-uwp](https://avaloniaui.net/blog/winui-vs-wpf-vs-uwp)  
23. Improving Performance | Avalonia Docs, fecha de acceso: diciembre 26, 2025, [https://docs.avaloniaui.net/docs/guides/development-guides/improving-performance](https://docs.avaloniaui.net/docs/guides/development-guides/improving-performance)  
24. 10 Avalonia Performance Tips to Supercharge Your App, fecha de acceso: diciembre 26, 2025, [https://avaloniaui.net/blog/10-avalonia-performance-tips-to-supercharge-your-app](https://avaloniaui.net/blog/10-avalonia-performance-tips-to-supercharge-your-app)  
25. AvaloniaUI \- What is the proper way to inject ViewModels into Views using composition-root based DI system?, fecha de acceso: diciembre 26, 2025, [https://stackoverflow.com/questions/71119254/avaloniaui-what-is-the-proper-way-to-inject-viewmodels-into-views-using-compos](https://stackoverflow.com/questions/71119254/avaloniaui-what-is-the-proper-way-to-inject-viewmodels-into-views-using-compos)  
26. D2ArmorPicker V2.4.0 brings back the features you missed : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/13rngz5/d2armorpicker\_v240\_brings\_back\_the\_features\_you/](https://www.reddit.com/r/DestinyTheGame/comments/13rngz5/d2armorpicker_v240_brings_back_the_features_you/)  
27. Destiny 2 Armor Picker Guide \- NEW WEBSITE to Find Max Stats and Triple 100 Builds., fecha de acceso: diciembre 26, 2025, [https://www.youtube.com/watch?v=aKlmHC8jjoA](https://www.youtube.com/watch?v=aKlmHC8jjoA)  
28. Solving a Multiple Knapsacks Problem | OR-Tools \- Google for Developers, fecha de acceso: diciembre 26, 2025, [https://developers.google.com/optimization/pack/multiple\_knapsack](https://developers.google.com/optimization/pack/multiple_knapsack)  
29. The Definitive Edge of Fate Armor Stat Planner : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1l4ogzu/the\_definitive\_edge\_of\_fate\_armor\_stat\_planner/](https://www.reddit.com/r/DestinyTheGame/comments/1l4ogzu/the_definitive_edge_of_fate_armor_stat_planner/)  
30. google/or-tools: Google's Operations Research tools \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/google/or-tools](https://github.com/google/or-tools)  
31. Destiny 2 Armor 3.0 Explained \- Complete Edge of Fate Guide \- Boosting Ground, fecha de acceso: diciembre 26, 2025, [https://boosting-ground.com/Destiny2/guides/pve-guides/armor-3-0-complete-guide](https://boosting-ground.com/Destiny2/guides/pve-guides/armor-3-0-complete-guide)  
32. Character stat changes coming with Edge of Fate : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1kgibgz/character\_stat\_changes\_coming\_with\_edge\_of\_fate/](https://www.reddit.com/r/DestinyTheGame/comments/1kgibgz/character_stat_changes_coming_with_edge_of_fate/)  
33. In-depth Semantic Kernel Demos \- Microsoft Learn, fecha de acceso: diciembre 26, 2025, [https://learn.microsoft.com/en-us/semantic-kernel/get-started/detailed-samples](https://learn.microsoft.com/en-us/semantic-kernel/get-started/detailed-samples)  
34. Introduction to Semantic Kernel | Microsoft Learn, fecha de acceso: diciembre 26, 2025, [https://learn.microsoft.com/en-us/semantic-kernel/overview/](https://learn.microsoft.com/en-us/semantic-kernel/overview/)  
35. Adding Retrieval Augmented Generation (RAG) to Semantic Kernel Agents | Microsoft Learn, fecha de acceso: diciembre 26, 2025, [https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-rag](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-rag)  
36. Step by Step Guide on Building Agentic RAG with Microsoft Semantic Kernel and Azure AI Search | by Akshay Kokane | Data Science Collective | Medium, fecha de acceso: diciembre 26, 2025, [https://medium.com/data-science-collective/step-by-step-guide-on-building-agentic-rag-with-microsoft-semantic-kernel-and-azure-ai-search-3dcee5bf38ba](https://medium.com/data-science-collective/step-by-step-guide-on-building-agentic-rag-with-microsoft-semantic-kernel-and-azure-ai-search-3dcee5bf38ba)  
37. Semantic Kernel \- Stepwise Planner \- Microsoft Dev Blogs, fecha de acceso: diciembre 26, 2025, [https://devblogs.microsoft.com/semantic-kernel/semantic-kernel-planners-stepwise-planner/](https://devblogs.microsoft.com/semantic-kernel/semantic-kernel-planners-stepwise-planner/)  
38. Getting Started with Semantic Kernel and C\# \- Matt on ML.NET \- Accessible AI, fecha de acceso: diciembre 26, 2025, [https://accessibleai.dev/post/introtosemantickernel/](https://accessibleai.dev/post/introtosemantickernel/)  
39. How to quickly start with Semantic Kernel | Microsoft Learn, fecha de acceso: diciembre 26, 2025, [https://learn.microsoft.com/en-us/semantic-kernel/get-started/quick-start-guide](https://learn.microsoft.com/en-us/semantic-kernel/get-started/quick-start-guide)  
40. Function Calling with Open-Source LLMs \- BentoML, fecha de acceso: diciembre 26, 2025, [https://www.bentoml.com/blog/function-calling-with-open-source-llms](https://www.bentoml.com/blog/function-calling-with-open-source-llms)  
41. Destiny 2 Armour 3.0: Stat changes and archetypes in Edge of Fate \- PC Gamer, fecha de acceso: diciembre 26, 2025, [https://www.pcgamer.com/games/fps/destiny-2-armour-3-0-stats-archetypes/](https://www.pcgamer.com/games/fps/destiny-2-armour-3-0-stats-archetypes/)  
42. Armor 3.0 Stat Balance : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1l40tvg/armor\_30\_stat\_balance/](https://www.reddit.com/r/DestinyTheGame/comments/1l40tvg/armor_30_stat_balance/)