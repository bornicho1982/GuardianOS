# **Informe Técnico Exhaustivo: Reingeniería Algorítmica y Optimización Matemática para D2ArmorPicker en la Era de 'Armor 3.0' y 'The Edge of Fate'**

## **1\. Introducción y Alcance del Nuevo Paradigma**

La llegada de la expansión *Destiny 2: The Edge of Fate* en julio de 2025 ha marcado el punto de inflexión más significativo en la historia de la gestión de inventarios y la construcción de equipamiento ("buildcrafting") del juego.1 Durante años, herramientas comunitarias esenciales como D2ArmorPicker han operado bajo un conjunto de axiomas matemáticos conocidos colectivamente como "Armor 2.0". Este sistema se caracterizaba por una lógica de programación lineal entera centrada en umbrales discretos (breakpoints) de 10 puntos, un límite estricto de 100 por estadística y una fórmula de Obra Maestra (Masterwork) constante y predecible.3

Sin embargo, la implementación de la arquitectura "Armor 3.0" ha invalidado gran parte de la lógica algorítmica existente. La transición hacia un sistema de escala lineal de 1 a 200, la introducción de "Arquetipos" deterministas, la jerarquía de "Tiers" (Niveles) de equipamiento y, fundamentalmente, la mecánica de "Sintonización" (Tuning) en armaduras de Tier 5, exigen una reescritura completa de los motores de optimización.2

Este informe técnico tiene como objetivo descomponer, analizar y reformular las ecuaciones matemáticas que gobiernan D2ArmorPicker. El propósito es proporcionar una hoja de ruta matemática y lógica para modernizar la herramienta, asegurando que no solo sea compatible con los nuevos sistemas de *The Edge of Fate*, sino que aproveche las nuevas estructuras de datos para ofrecer una precisión y fiabilidad superiores. A través de este análisis, exploraremos cómo la complejidad combinatoria ha aumentado exponencialmente y propondremos soluciones algorítmicas, como la ramificación y poda (Branch and Bound) y el uso de máscaras de bits, para mantener la eficiencia en entornos de navegador.

## ---

**2\. Ontología Estadística de Armor 3.0: Redefinición de las Variables Objetivo**

Para reconstruir los algoritmos de optimización, primero debemos establecer una definición rigurosa de las nuevas entidades de datos. La suposición fundamental de que "las estadísticas son cubos discretos de 10 unidades" ha sido eliminada. El nuevo paradigma se define por dos principios matemáticos: **Granularidad Lineal** y **Techos Extendidos (Extended Ceilings)**.

### **2.1 Mapeo Semántico y Curvas de Utilidad**

La transición de Armor 2.0 a 3.0 implica un mapeo semántico directo de las antiguas estadísticas a nuevas identidades funcionales. Sin embargo, las curvas de utilidad —la función $f(x)$ que determina el beneficio en el juego dado un valor de estadística $x$— han cambiado drásticamente. Las herramientas de optimización deben actualizar no solo su interfaz de usuario, sino también la ponderación interna de estas variables en la función objetivo.

A continuación, se presenta la tabla de conversión y la nueva lógica funcional que el algoritmo debe respetar:

Tabla 1: Conversión de Estadísticas y Funcionalidad Matemática 5

| Estadística Legada (Li​) | Nueva Estadística (Ni​) | Símbolo (xi​) | Función Matemática y Prioridad de Optimización |
| :---- | :---- | :---- | :---- |
| **Movilidad** | **Armas (Weapons)** | $S\_W$ | *Función:* Manejo, Recarga, Daño PvE vs Menores. *Cambio:* Ya no es específica de clase para Cazadores. Ahora es una utilidad universal de DPS. |
| **Resiliencia** | **Salud (Health)** | $S\_H$ | *Función:* Resistencia al estremecimiento (Flinch), Curación por Orbes, Capacidad de Escudo. *Cambio:* Escala la capacidad del escudo por encima de 100\. Prioridad crítica para supervivencia. |
| **Recuperación** | **Clase (Class)** | $S\_C$ | *Función:* Regeneración de Habilidad de Clase, Sobreescudos. *Cambio:* Desacoplada de la regeneración de salud base. Ahora es puramente regeneración de habilidad \+ utilidad. |
| **Disciplina** | **Granada (Grenade)** | $S\_G$ | *Función:* Enfriamiento de Granada. *Nuevo:* Escalar de Daño ($\>100$). Prioridad alta para builds de "spam" de habilidades. |
| **Intelecto** | **Súper (Super)** | $S\_S$ | *Función:* Regeneración de Súper. *Nuevo:* Escalar de Daño de Súper ($\>100$). Crítico para fases de DPS en Incursiones. |
| **Fuerza** | **Cuerpo a Cuerpo (Melee)** | $S\_M$ | *Función:* Enfriamiento de Melee. *Nuevo:* Escalar de Daño de Melee ($\>100$). |

### **2.2 Análisis de la Función Objetivo $f(x)$**

En el sistema anterior, la función de beneficio era una función escalonada (step function):

$$f\_{legacy}(x) \= \\lfloor \\frac{x}{10} \\rfloor$$

Esto implicaba que un valor de $x=69$ era matemáticamente idéntico a $x=60$. Esto permitía a D2ArmorPicker utilizar técnicas de "poda" agresivas, ignorando cualquier desperdicio.  
En Armor 3.0, la función es continua y lineal por tramos, con una discontinuidad en la derivada en $x=100$:

$$f\_{new}(x) \= \\begin{cases} k\_1 \\cdot x & \\text{si } 0 \\le x \\le 100 \\\\ k\_1 \\cdot 100 \+ k\_2 \\cdot (x \- 100\) & \\text{si } 100 \< x \\le 200 \\end{cases}$$  
Donde $k\_1$ representa el coeficiente de beneficio (ej. reducción de enfriamiento) y $k\_2$ representa el coeficiente de bonificación secundaria (ej. aumento de daño).2

Implicación Algorítmica: El concepto de "Desperdicio de Estadísticas" (Wasted Stats) debe ser redefinido. Anteriormente, el desperdicio era $W \= x \\mod 10$. Ahora, el desperdicio es técnicamente cero, ya que $143 \> 142$. Sin embargo, el algoritmo debe permitir al usuario definir Pesos de Utilidad. Es posible que un usuario valore $k\_2$ (daño extra) mucho más que $k\_1$ (enfriamiento). Por lo tanto, el solver no debe buscar simplemente "llegar a Tier 10", sino maximizar una suma ponderada vectorial:

$$\\text{Maximizar } Z \= \\sum\_{i=1}^{6} w\_i \\cdot f\_{new}(S\_i)$$

## ---

**3\. Modelado Matemático del Sistema de Tiers y Arquetipos**

El sistema anterior trataba a todas las armaduras legendarias como iguales en potencial (con totales de estadísticas rondando los 68 puntos). Armor 3.0 introduce una jerarquía estricta que actúa como un pre-filtro esencial para la optimización computacional. Ignorar este sistema resultaría en un espacio de búsqueda innecesariamente grande.

### **3.1 La Jerarquía de Tiers como Filtro de Poda**

La investigación indica cinco niveles de armadura, cada uno con rangos de estadísticas base y capacidades de energía de mods distintos 2:

* **Tier 1:** Total 48–53.  
* **Tier 2:** Total 53–58. (Nivel de Exóticas estándar).  
* **Tier 3:** Total 59–64.  
* **Tier 4 (T4):** Total 65–72. **11 de Energía de Mods**.  
* **Tier 5 (T5):** Total 73–75. **11 de Energía \+ Ranura de Sintonización (Tuning Slot)**.

Implicación Matemática: El máximo potencial de estadísticas totales ($Max\_{Total}$) ya no es uniforme para cualquier permutación.

$$Max\_{Total} \= \\sum\_{slot=1}^{5} (Base\_{slot} \+ MW\_{slot} \+ Mod\_{slot} \+ Tune\_{slot})$$

En esta ecuación, $Base\_{slot}$ y $Tune\_{slot}$ son funciones directas del Tier del ítem.  
Un algoritmo moderno debe priorizar los ítems Tier 5 no solo por sus estadísticas base superiores, sino por la variable $Tune\_{slot}$, que introduce una flexibilidad combinatoria crítica (discutida en profundidad en la Sección 5). Para un usuario que busca "Triple 100" (o ahora, valores de 200), el algoritmo puede **descartar heurísticamente** cualquier permutación que contenga más de $N$ piezas de Tier \< 4, ya que es matemáticamente imposible alcanzar ciertos umbrales sin la densidad de estadísticas de los Tiers altos.

### **3.2 Arquetipos Deterministas y Reducción del Espacio de Búsqueda**

En Armor 2.0, la distribución de estadísticas era pseudo-aleatoria (agrupada en *plugs* de Mob/Res/Rec y Disc/Int/Str). En Armor 3.0, la distribución es **Determinista basada en Arquetipos**.4

Cada pieza de armadura pertenece a uno de seis arquetipos. Esto define estrictamente sus picos de estadísticas:

1. **Estadística Primaria ($S\_{pri}$):** Valor base hasta 30\.  
2. **Estadística Secundaria ($S\_{sec}$):** Valor base hasta 25\.  
3. **Estadística Terciaria ($S\_{tert}$):** Aleatoria, valor hasta 20\.  
4. **Resto ($S\_{rest}$):** Valores cercanos a cero (antes de Masterwork).

Tabla 2: Definición de Arquetipos para Lógica de Filtrado 6

| Arquetipo | Primaria (Max 30\) | Secundaria (Max 25\) | Implicación para el Solver |
| :---- | :---- | :---- | :---- |
| **Gunner** | Armas ($S\_W$) | Granada ($S\_G$) | Esencial para builds de DPS con armas y spam de granadas. |
| **Brawler** | Melee ($S\_M$) | Salud ($S\_H$) | Crítico para titanes de combate cercano y supervivencia. |
| **Specialist** | Clase ($S\_C$) | Armas ($S\_W$) | Optimiza rotaciones de habilidad de clase y manejo de armas. |
| **Grenadier** | Granada ($S\_G$) | Súper ($S\_S$) | El arquetipo "Mago": máximo daño de habilidad y súper. |
| **Paragon** | Súper ($S\_S$) | Melee ($S\_M$) | Prioridad para generar súper rápidamente vía melee. |
| **Bulwark** | Salud ($S\_H$) | Clase ($S\_C$) | El arquetipo "Tanque": máxima resistencia y escudos. |

Optimización mediante Heurística de Grafos:  
D2ArmorPicker solía comprobar cada ítem contra cada permutación. Con los Arquetipos, podemos implementar Poda Heurística (Heuristic Pruning).

* *Escenario:* El usuario solicita $S\_{Super} \\ge 180$.  
* *Lógica:* Matemáticamente, para alcanzar 180 en una sola estadística base, se requiere una densidad extrema. Si el usuario no selecciona suficientes piezas *Paragon* o *Grenadier* (que contienen Súper como primaria o secundaria), es imposible alcanzar el objetivo.  
* *Algoritmo:* El sistema puede pre-calcular el "Máximo Teórico" de Súper disponible en el inventario actual. Si $\\sum Max(S\_{Super}) \< 180$, se devuelve un error inmediato sin iterar, ahorrando ciclos de CPU.

## ---

**4\. Formulación Avanzada del Algoritmo de Obra Maestra (Masterwork)**

Uno de los cambios más sutiles pero impactantes es la fórmula de Masterwork. En Armor 2.0, esto era una constante simple ($+2$ a todo). En Armor 3.0, es una función **condicional y dinámica**.12

### **4.1 La Regla de los "Tres Más Bajos"**

Según la investigación, al llevar una armadura a Nivel 5 (Masterwork), se otorga \+1 por nivel (total \+5) a las **tres estadísticas más bajas** de la pieza.

Sea $A$ una pieza de armadura con un vector de estadísticas base $V\_A \= \\{v\_1, v\_2,..., v\_6\\}$.  
Definimos $L\_{indices}$ como el conjunto de índices correspondientes a los tres valores más pequeños en $V\_A$. En caso de empate, el juego utiliza un orden determinista interno (probablemente basado en el índice del hash de la estadística).  
El vector de bonificación de Masterwork $M\_A$ para un ítem de nivel 5 se define como:

$$M\_A\[i\] \= \\begin{cases} 5 & \\text{if } i \\in L\_{indices} \\\\ 0 & \\text{otherwise} \\end{cases}$$

### **4.2 Conflicto Lógico y Resolución en Código**

Existe un desafío importante al mezclar armaduras "Legadas" (Legacy) con armaduras "Armor 3.0".

* **Armaduras Legadas:** Mantienen su distribución antigua, pero probablemente se mapean a las nuevas estadísticas. Su Masterwork sigue siendo $+2$ a todo (o $+12$ total).  
* **Armaduras Nuevas (Tier 1-5):** Siguen la regla de Arquetipos. Dado que los Arquetipos fuerzan picos en 2 o 3 estadísticas, las "tres más bajas" serán casi invariablemente las estadísticas que **no** pertenecen al arquetipo.

Consecuencia para la Optimización:  
El Masterwork en Armor 3.0 actúa como un Filtro de Normalización. En lugar de aumentar los picos (haciendo los valores altos aún más altos), eleva el "suelo" de las estadísticas basura. Esto aplana la curva de distribución total.

* *Implicación:* Es matemáticamente más difícil lograr picos extremos (ej. 200 en una estadística) confiando solo en el Masterwork. El algoritmo debe depender más de los Mods de Estadísticas y la Sintonización (Tuning) para alcanzar los máximos, mientras que el Masterwork ayuda a evitar ceros en las estadísticas secundarias.

## ---

**5\. Complejidad Algorítmica de la "Sintonización" (Tuning Slot)**

La introducción del **Mod de Sintonización** en armaduras de Tier 5 representa el aumento más significativo en la complejidad computacional para D2ArmorPicker.4

### **5.1 La Variable de Sintonización como Máquina de Estados**

En Armor 2.0, un ítem era un vector constante. En Armor 3.0, un ítem de Tier 5 es una **máquina de estados** con múltiples salidas potenciales que el usuario puede configurar.

Para cada ítem de Tier 5 ($t$), existen dos estrategias de sintonización mutuamente excluyentes:

1. **Sintonización Enfocada (Focused Tuning):** Otorgar $+5$ a la "Estadística Sintonizada" (fija por el *roll* del ítem) y aplicar $-5$ a otra estadística (variable a elección del usuario).  
2. **Sintonización Balanceada (Balanced Tuning):** Otorgar $+1$ a las tres estadísticas más bajas (variable según la distribución base).

### **5.2 Explosión Combinatoria**

Sea $T$ el conjunto de ítems Tier 5 en una permutación de equipamiento. Para cada $t \\in T$:

* Si elegimos *Sintonización Enfocada*, hay potencialmente 5 opciones para aplicar la penalización de $-5$ (cualquier estadística excepto la sintonizada).  
* Si elegimos *Sintonización Balanceada*, es un estado único.  
* Total de estados por ítem Tier 5 $\\approx 6$.

Si un equipamiento completo consta de 5 piezas Tier 5, el número de permutaciones para las estadísticas de la armadura *solamente* (antes de mezclar diferentes piezas de armadura) aumenta por un factor de $6^5 \= 7,776$. Esto es inaceptable para un cálculo en tiempo real en el navegador si se aplica por fuerza bruta.

### **5.3 Formulación de Restricciones y Solución**

La investigación indica que la estadística que recibe el $+5$ está determinada por el RNG o la "Sintonización de Atributo" (Attunement) al caer el ítem.12 Esto simplifica ligeramente el grafo: no elegimos el $+5$, solo elegimos el $-5$.

**Pseudocódigo para la Integración Lógica de la Sintonización:**

El algoritmo no debe iterar sobre las 7,776 combinaciones por set. Debe usar una aproximación "Greedy" o de Ramificación dentro de la validación del set.

Python

def calcular\_permutaciones\_optimizadas(lista\_items):  
    for permutacion in generar\_sets\_base(lista\_items):  
        \# permutacion contiene 5 items. Algunos pueden ser Tier 5\.  
        items\_t5 \= \[item for item in permutacion if item.tier \== 5\]  
          
        if not items\_t5:  
            yield permutacion \# Caso simple  
            continue  
              
        \# Resolver sub-problema de Sintonización para esta permutación  
        \# Objetivo: Maximizar la utilidad según los pesos del usuario  
          
        estado\_actual \= suma\_base(permutacion)  
          
        for item in items\_t5:  
            mejor\_opcion \= evaluar\_mejor\_tuning(item, estado\_actual, preferencias\_usuario)  
            aplicar\_tuning(estado\_actual, mejor\_opcion)  
              
        if cumple\_requisitos(estado\_actual):  
            yield estado\_actual

La función evaluar\_mejor\_tuning debe determinar si es más beneficioso aplicar $+1/+1/+1$ (para rellenar huecos) o $+5/-5$ (para alcanzar un pico de 100 o 200). Dado que es una decisión local por ítem, se reduce la complejidad de $6^N$ a $6 \\cdot N$.

## ---

**6\. Modernización de la Función Objetivo: Del Problema de la Mochila a la Maximización Vectorial**

El núcleo de D2ArmorPicker es encontrar la "mejor" combinación. Anteriormente, esto era un Problema de la Mochila (Knapsack Problem) resolviendo para máximos Tiers (decenas). Ahora, es un **Problema de Maximización Vectorial**.

### **6.1 El Vector Objetivo y la Restricción de Energía**

El usuario define un vector objetivo $V\_{obj} \= \\{v\_1, v\_2,..., v\_6\\}$.  
El algoritmo busca un equipamiento $L$ tal que:

$$S\_{total}(L) \\ge V\_{obj}$$  
Sin embargo, con la capacidad de 11 de energía en armaduras Tier 4/5 2, el costo de los mods se convierte en una variable de restricción más flexible pero compleja.

Lógica Legada: $Costo(Mods) \\le 10$ por pieza.  
Nueva Lógica 3.0:

$$\\sum Costo(ModsCombate) \+ \\sum Costo(ModsEstadistica) \\le \\sum Capacidad(L\_i)$$

Donde $Capacidad(L\_i) \= 11$ si $Tier \\ge 4$, de lo contrario 10 (o menos).  
Esto permite combinaciones previamente imposibles, como usar Mods de Estadística Mayor (+10, costo 3\) junto con mods de estilo de combate costosos en la misma pieza, gracias a ese punto extra de energía.

Oportunidad de Optimización (Bin Packing):  
El algoritmo puede asignar dinámicamente mods de estadística estándar (+5/+10) a las ranuras con energía disponible. Esto es un sub-problema de Empaquetado de Contenedores (Bin Packing).

* *Entrada:* Aumento de estadística requerido $\\Delta S \= V\_{obj} \- S\_{base}$.  
* *Contenedores:* 5 ranuras de armadura con energía restante $E\_{rem, i} \= Capacidad\_i \- Costo(ModsFijos\_i)$.  
* *Ítems:* Mods Mayores (+10, costo 3), Mods Menores (+5, costo 1).

El solver debe verificar si los mods de estadística necesarios para alcanzar el objetivo $V\_{obj}$ pueden "caber" en la energía restante de la permutación de armadura seleccionada.

## ---

**7\. Integración de Bonificaciones de Set (El Aspecto de Cobertura de Conjuntos)**

"The Edge of Fate" introduce Bonificaciones de Set (ej. *Bushido*, *Techsec*) que requieren equipar 2 o 4 piezas del mismo conjunto.14 Esto añade una capa de **Satisfacibilidad Booleana (SAT)** a la optimización.

### **7.1 Representación Matemática y Filtrado**

Sea $Set\_K$ un conjunto de armadura específico (ej. Bushido).  
Sea $n\_K$ el conteo de ítems en la permutación actual que pertenecen a $Set\_K$.  
Requisito del usuario: "Debe tener bonificación de 4 piezas de Bushido".  
Restricción: $n\_{Bushido} \\ge 4$.  
Esto actúa como un **filtro estricto** antes del motor de permutación, lo cual es excelente para el rendimiento.

1. **Agrupar por Ranura:** Separar la armadura disponible en subconjuntos basados en su armorSetHash.  
2. **Verificación Combinatoria:** Si el usuario requiere 4 piezas de Bushido, el solver debe bloquear 4 ranuras (ej. Casco, Brazos, Pecho, Piernas) exclusivamente para ítems con la etiqueta "Bushido".  
3. **Ranura Flexible:** Solo la 5ª ranura (o la exótica, si se permite) es libre para recorrer el grafo de inventario completo.

Peso de "Estadística Virtual":  
Los usuarios avanzados querrán ponderar las bonificaciones de set frente a las estadísticas brutas.

* *Ejemplo:* "¿Vale la pena la reducción de daño de Bushido (4 piezas) a costa de perder 20 puntos de Salud?"  
* Para resolver esto, el algoritmo puede asignar un **Valor Heurístico** a la bonificación de set (ej. $Valor\_{Bushido} \\approx 30$ puntos virtuales). El solver maximiza entonces $Puntaje \= \\sum Estadísticas \+ Valor\_{Set}$.

## ---

**8\. Subclase Prismática: Combinatoria de Fragmentos**

La subclase Prismática 15 permite mezclar fragmentos de Luz y Oscuridad, muchos de los cuales conllevan penalizaciones o bonificaciones de estadísticas (ej. Facet of Solitude, Facet of Honor).

### **8.1 Variabilidad de Ranuras de Fragmentos**

A diferencia de las subclases anteriores donde los Aspectos determinaban rígidamente las ranuras (3-5 total), Prismática ha tenido ajustes recientes en la cantidad de ranuras por Aspecto (algunos revertidos de 1 a 2).17 D2ArmorPicker debe consultar dinámicamente la definición del Aspecto para saber cuántas ranuras $F\_{slots}$ están disponibles.

Contribución Total de Fragmentos:

$$S\_{frag} \= \\sum\_{f \\in FragmentosSeleccionados} Valor(f)$$

### **8.2 El Problema de la Mochila en Fragmentos**

Si el usuario selecciona "Auto-optimizar Fragmentos", la herramienta debe resolver un problema de la mochila:

* *Capacidad:* $F\_{slots}$ (ej. 5 ranuras).  
* *Ítems:* Todos los Fragmentos Prismáticos disponibles.  
* *Peso:* 1 ranura por fragmento.  
* *Valor:* Densidad de estadísticas del fragmento (+10, \+20, \-10).

Sin embargo, la mayoría de los usuarios definen los fragmentos como **restricciones fijas** por sus efectos de jugabilidad ("Necesito Facet of Bravery para Volatile Rounds"). En este caso, la herramienta simplemente calcula el desplazamiento estático resultante y lo aplica a la ecuación $S\_{total}$.

## ---

**9\. Propuesta de Arquitectura de Datos API y Estructuras JSON**

Para soportar estos cambios matemáticos, la tubería de ingestión de datos desde la API de Bungie debe ser reformada. D2ArmorPicker actualmente analiza DestinyInventoryItemDefinition. Ahora debe incorporar campos adicionales específicos de Armor 3.0.18

### **9.1 Nuevas Propiedades del Manifiesto**

Basado en las actualizaciones de la documentación de la API, los siguientes campos son críticos:

1. **gearTier**: Identificador entero (1–5).  
   * *Lógica:* Si gearTier está ausente o es null, asumir que es Armadura Legada (Armor 2.0).  
   * *Lógica:* Si gearTier \== 5, habilitar la lógica de Sintonización en la UI y el solver.  
2. **tuningStatHash**: Para ítems Tier 5, este hash identifica qué estadística está bloqueada para recibir el potencial de \+5 (Atributo Sintonizado). Esto es vital para no ofrecer al usuario sintonizaciones imposibles.  
3. **armorArchetype**: (ej. "Gunner", "Paragon"). Útil para ordenamiento en la UI y filtrado heurístico.  
4. **armorSetHash**: Identifica la pertenencia a conjuntos como "Bushido" o "Techsec".

### **9.2 Modelo de Objeto JSON Interno Propuesto**

La herramienta debe convertir los datos crudos de la API en un formato JSON interno ligero optimizado para el solver:

JSON

{  
  "id": "6917529062061293645",  
  "hash": 123456789,  
  "tier": 5,  
  "archetype": "Paragon",  
  "is\_legacy": false,  
  "base\_stats": {   
      "weapons": 2, "health": 2, "class": 2,   
      "grenade": 2, "super": 30, "melee": 25   
  },  
  "masterwork\_bonus\_vector": , // Pre-calculado usando la regla "Lowest 3"  
  "tuning\_attunement\_stat": "super", // La estadística elegible para \+5 fijo  
  "set\_bonus\_id": "bushido",  
  "energy\_capacity": 11,  
  "artifice\_slot": false // False para Tier 5, True para Legacy Artifice  
}

**Nota sobre Pre-cálculo:** Dado que el bono de Masterwork depende de las estadísticas base *internas* del ítem (que son estáticas), el vector de $+5$ debe pre-calcularse durante la fase de "Descarga de Inventario", ahorrando millones de operaciones de comparación durante la fase de "Permutación".

## ---

**10\. Estrategia de Algoritmos de Optimización: Branch and Bound con Máscaras de Bits**

Dado el aumento de complejidad (Sintonización \+ Escala Lineal), un bucle de fuerza bruta $O(n^5)$ es arriesgado para el rendimiento en JavaScript. Se propone un enfoque de **Ramificación y Poda (Branch and Bound)**.

### **10.1 Máscaras de Bits para Arquetipos y Sets**

Podemos representar los requisitos de Arquetipo y Set usando máscaras de bits para comparaciones ultra-rápidas.

* Sea Gunner \= 000001, Brawler \= 000010\.  
* Sea Bushido \= 00000001, Techsec \= 00000010\.  
* Las operaciones de filtrado se convierten en operaciones bitwise AND (&) en lugar de comparaciones de cadenas o búsquedas en arrays.

### **10.2 Algoritmo Branch and Bound**

En lugar de generar todas las permutaciones y luego filtrarlas, mantenemos un "Potencial Máximo" corriente para las ranuras restantes.

1. **Ordenar** los ítems en cada ranura (Casco, Guanteletes, etc.) por Suma Total de Estadísticas (descendente).  
2. **Seleccionar** un Casco. Calcular la suma parcial de estadísticas.  
3. **Acotar (Bound):** Calcular la adición máxima posible de las ranuras restantes (Brazos, Pecho, Piernas, Clase), asumiendo que son piezas Tier 5 "perfectas" con sintonización ideal.  
4. **Poda (Prune):** Si Suma\_Parcial\_Actual \+ Maximo\_Restante \< Objetivo\_Usuario, detener esta rama inmediatamente. No iterar sobre los brazos/pecho/piernas para este casco, ya que es matemáticamente imposible que cumplan el objetivo.

### **10.3 Manejo de la "Sintonización" en la Fase de Acotación**

La ranura de Sintonización añade un "Potencial Flotante" de $+5$ o $+3$.

* En la fase de acotación, el algoritmo debe asumir el **Escenario del Mejor Caso** para la sintonización (ej. asumir que siempre podemos sintonizar la estadística que el usuario más necesita).  
* Solo cuando una rama llega a un nodo hoja (un set completo seleccionado) y pasa la verificación de umbral preliminar, se resuelve la restricción específica de Sintonización ($+5$ vs $-5$) para validar el set final.

## ---

**11\. Compatibilidad con Legado y Lógica de Migración**

Una parte significativa de la base de usuarios poseerá inventarios mixtos (Artifice Legado \+ Nuevo Tier 5). El sistema debe manejar esto con elegancia.

### **11.1 La Falacia del "Combo Ilegal"**

La investigación menciona "Combos Ilegales" en Armor 2.0 (estadísticas que no podían aparecer juntas debido a los plugs de estadísticas compartidos). Los Arquetipos de Armor 3.0 formalizan esto, pero de manera diferente.

* *Armadura Legada:* Permite distribuciones imposibles en 3.0 (ej. división alta de Int/Disc podría ser rara ahora si los Arquetipos fuerzan emparejamientos específicos).  
* **Recomendación:** El algoritmo **no debe** forzar la lógica de Arquetipos sobre ítems Legados. Debe tratar los ítems Legados como "Arquetipo: Neutral" y usar sus valores de estadísticas crudos leídos de la API.

### **11.2 La Excepción del Ítem de Clase Exótico**

Los Ítems de Clase Exóticos en *Edge of Fate* tienen estadísticas fijas (30/20/13) determinadas por sus perks (rasgos).19

* *Atajo de Optimización:* El solver no necesita permutar Ítems de Clase Exóticos por sus estadísticas. Solo necesita permutarlos por sus *Perks*.  
* Si el usuario selecciona "Spirit of the Assassin", las estadísticas son matemáticamente constantes. Esto elimina la ranura de Ítem de Clase del grafo de permutación de estadísticas, reduciendo la complejidad por un factor de $N\_{class\\\_items}$.

### **11.3 Artifice vs Tier 5**

El algoritmo debe comparar la utilidad de una Armadura Artifice Legada (Bono \+3 libre, stats base \~68) vs Tier 5 (Bono \+5/-5 restringido, stats base 75).

* Para builds que necesitan "rellenar" un hueco pequeño en una estadística específica sin penalizar otra, Artifice sigue siendo superior.  
* Para builds que buscan maximizar el total bruto (Tier 40+ equivalente), Tier 5 es matemáticamente superior debido a la base más alta y los 11 de energía.

## ---

**12\. Conclusión y Recomendaciones Finales**

La modernización de D2ArmorPicker para la era de *The Edge of Fate* no es una simple actualización de valores; es un cambio de paradigma desde la **Programación Lineal Entera (Cubos)** a la **Satisfacción de Restricciones con Variables Dinámicas**.

**Pasos Accionables Clave para el Desarrollo:**

1. **Reescribir el Motor de Estadísticas:** Deprecar floor(x/10). Implementar min(x, 200\) y sumatoria lineal.  
2. **Implementar Conciencia de Tiers:** Añadir lógica para detectar Tier 5 y desbloquear el "Sub-problema de Sintonización" en el solver.  
3. **Pre-calcular Masterworks:** Mover el cálculo de Masterwork a la fase de importación de datos usando la regla de "Los 3 Más Bajos".  
4. **Vectorizar Costos de Mods:** Actualizar la lógica de asignación de mods para manejar ranuras de 11 de energía y el empaquetado (bin-packing) de mods costosos.  
5. **Revisión de UI:** Eliminar selectores de "Tier T1-T32". Reemplazar con deslizadores de "Valor Objetivo de Estadística" (0-200) y selectores de preferencia de Arquetipo.

Al adoptar estos cambios matemáticos y arquitectónicos, la herramienta no solo restaurará su precisión para la expansión *Edge of Fate*, sino que aprovechará la naturaleza determinista de los Arquetipos para proporcionar recomendaciones de construcción más rápidas, específicas y poderosas que nunca.

### ---

**Apéndice A: Resumen de Fórmulas de Referencia**

* Nuevo Total de Estadística ($S\_{tot}$):

  $$S\_{tot, i} \= \\sum\_{p \\in Slots} (Base\_{p,i} \+ MW\_{p,i} \+ Mod\_{p,i} \+ Tune\_{p,i}) \+ Frag\_i \+ Art\_i$$  
* Vector de Masterwork ($MW\_p$):

  $$MW\_{p,i} \= 5 \\iff i \\in Indices(SortAsc(Base\_p)\[0..2\])$$  
* Restricción de Sintonización Tier 5 ($Tune\_{T5}$):

  $$Tune\_{T5} \\in \\{ (+1, \+1, \+1)\_{lowest}, (+5\_{tuned}, \-5\_{variable}) \\}$$  
* Restricción de Energía de Mods:

  $$\\sum Costos \\le 55 \\quad (\\text{si todas las piezas son Tier 4+})$$

---

*Fin del Informe Técnico.*

#### **Obras citadas**

1. fecha de acceso: diciembre 26, 2025, [https://en.wikipedia.org/wiki/Destiny\_2:\_The\_Edge\_of\_Fate](https://en.wikipedia.org/wiki/Destiny_2:_The_Edge_of_Fate)  
2. Destiny 2 Armor 3.0 Guide – Tier System, Stats, Sets & Bonuses \- Skycoach, fecha de acceso: diciembre 26, 2025, [https://skycoach.gg/blog/destiny/articles/armor-3-0-guide](https://skycoach.gg/blog/destiny/articles/armor-3-0-guide)  
3. D2ArmorPicker, fecha de acceso: diciembre 26, 2025, [https://d2armorpicker.com/](https://d2armorpicker.com/)  
4. Destiny 2 Armor 3.0 Explained \- Complete Edge of Fate Guide \- Boosting Ground, fecha de acceso: diciembre 26, 2025, [https://boosting-ground.com/Destiny2/guides/pve-guides/armor-3-0-complete-guide](https://boosting-ground.com/Destiny2/guides/pve-guides/armor-3-0-complete-guide)  
5. Destiny 2 Stat Changes in Edge of Fate Explained \- IGN, fecha de acceso: diciembre 26, 2025, [https://www.ign.com/wikis/destiny-2/Destiny\_2\_Stat\_Changes\_in\_Edge\_of\_Fate\_Explained](https://www.ign.com/wikis/destiny-2/Destiny_2_Stat_Changes_in_Edge_of_Fate_Explained)  
6. New Armor Explained (Plus Spreadsheet) : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1l4f4jy/new\_armor\_explained\_plus\_spreadsheet/](https://www.reddit.com/r/DestinyTheGame/comments/1l4f4jy/new_armor_explained_plus_spreadsheet/)  
7. Armor 3.0 Explained in 12 Minutes (Destiny 2 \- Edge of Fate) \- YouTube, fecha de acceso: diciembre 26, 2025, [https://www.youtube.com/watch?v=FMEQESvv2U8](https://www.youtube.com/watch?v=FMEQESvv2U8)  
8. Spreadsheet for tracking T5 armours. : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1n1p36u/spreadsheet\_for\_tracking\_t5\_armours/](https://www.reddit.com/r/DestinyTheGame/comments/1n1p36u/spreadsheet_for_tracking_t5_armours/)  
9. Armor Tier Differences : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1l2odpd/armor\_tier\_differences/](https://www.reddit.com/r/DestinyTheGame/comments/1l2odpd/armor_tier_differences/)  
10. Destiny 2 Armour 3.0: Stat changes and archetypes in Edge of Fate \- PC Gamer, fecha de acceso: diciembre 26, 2025, [https://www.pcgamer.com/games/fps/destiny-2-armour-3-0-stats-archetypes/](https://www.pcgamer.com/games/fps/destiny-2-armour-3-0-stats-archetypes/)  
11. The Definitive Edge of Fate Armor Stat Planner : r/destiny2builds \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/destiny2builds/comments/1l4oh3d/the\_definitive\_edge\_of\_fate\_armor\_stat\_planner/](https://www.reddit.com/r/destiny2builds/comments/1l4oh3d/the_definitive_edge_of_fate_armor_stat_planner/)  
12. The Definitive Edge of Fate Armor Stat Planner : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1l4ogzu/the\_definitive\_edge\_of\_fate\_armor\_stat\_planner/](https://www.reddit.com/r/DestinyTheGame/comments/1l4ogzu/the_definitive_edge_of_fate_armor_stat_planner/)  
13. Considering how armor 3.0 rolls work, stat penalty on stats below 30 should be entirely removed : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1makkmo/considering\_how\_armor\_30\_rolls\_work\_stat\_penalty/](https://www.reddit.com/r/DestinyTheGame/comments/1makkmo/considering_how_armor_30_rolls_work_stat_penalty/)  
14. Destiny 2: Edge of Fate All Armor Set Bonuses \- PlayerAuctions, fecha de acceso: diciembre 26, 2025, [https://www.playerauctions.com/destiny-2-guide/tips-guides/edge-of-fate-all-armor-set-bonuses/](https://www.playerauctions.com/destiny-2-guide/tips-guides/edge-of-fate-all-armor-set-bonuses/)  
15. Prismatic Fragments List and Locations \- Destiny 2 Guide \- IGN, fecha de acceso: diciembre 26, 2025, [https://www.ign.com/wikis/destiny-2/Prismatic\_Fragments\_List\_and\_Locations](https://www.ign.com/wikis/destiny-2/Prismatic_Fragments_List_and_Locations)  
16. Destiny 2: All Prismatic Fragments \- Turtle Beach, fecha de acceso: diciembre 26, 2025, [https://www.turtlebeach.com/blog/destiny-2-all-prismatic-fragments](https://www.turtlebeach.com/blog/destiny-2-all-prismatic-fragments)  
17. Re: Prismatic Subclass Tuning \- Fragments : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1l39t4z/re\_prismatic\_subclass\_tuning\_fragments/](https://www.reddit.com/r/DestinyTheGame/comments/1l39t4z/re_prismatic_subclass_tuning_fragments/)  
18. Resources for the Bungie.net API \- GitHub, fecha de acceso: diciembre 26, 2025, [https://github.com/Bungie-net/api](https://github.com/Bungie-net/api)  
19. Do we know what will happen to our current class items with EoF? Will they get random stat rolls? : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1lt76ei/do\_we\_know\_what\_will\_happen\_to\_our\_current\_class/](https://www.reddit.com/r/DestinyTheGame/comments/1lt76ei/do_we_know_what_will_happen_to_our_current_class/)  
20. Class Items in EoF : r/DestinyTheGame \- Reddit, fecha de acceso: diciembre 26, 2025, [https://www.reddit.com/r/DestinyTheGame/comments/1ltotjr/class\_items\_in\_eof/](https://www.reddit.com/r/DestinyTheGame/comments/1ltotjr/class_items_in_eof/)