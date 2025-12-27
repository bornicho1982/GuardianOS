# **Arquitectura Técnica: Generador de Builds con IA para Destiny 2 "The Edge of Fate"**

## **1\. Visión General del Sistema: RAG (Retrieval-Augmented Generation)**

Para crear una IA que genere la "build perfecta", no basta con un modelo de lenguaje (LLM) genérico como GPT-4, ya que estos no conocen tu inventario específico ni los últimos cambios de "The Edge of Fate" (como la Armadura Tier 5 y el Tuning). La solución es una arquitectura **RAG (Retrieval-Augmented Generation)**.

Este sistema no "alucina" builds; consulta datos reales. Funciona en tres pasos:

1. **Recuperación (Retrieval):** Busca en tu base de datos vectorial (tu inventario \+ meta actual).  
2. **Aumentación (Augmentation):** Inyecta esos datos en el prompt del LLM.  
3. **Generación (Generation):** El LLM escribe la respuesta final usando *solo* lo que tú tienes.

## ---

**2\. Componentes de la Arquitectura**

### **2.1 La Base de Conocimiento (Knowledge Base)**

Necesitas dos fuentes de verdad que la IA consultará:

1. **El Manifiesto de Bungie (Estático):**  
   * **Contenido:** Definiciones de todos los Mods, Fragmentos Prismáticos, Perks de Armas y Exóticos.  
   * **Procesamiento:** Debes descargar el Manifiesto (SQLite), extraer las tablas relevantes (DestinyInventoryItemDefinition, DestinySandboxPerkDefinition) y convertirlas a texto semántico.  
   * *Ejemplo de vectorización:* "Facet of Bravery: Otorga Volatile Rounds a armas de Vacío al derrotar objetivos con granadas."  
2. **El "Meta" (Dinámico):**  
   * **Fuente:** Debes "scrapear" o ingerir datos de fuentes comunitarias confiables como **Mobalytics**, **Light.gg** o hojas de cálculo de creadores de contenido (ej. Aegis/Court mencionados en la investigación) que definen qué es "meta" en la expansión *Edge of Fate*.  
   * **Formato:** JSON/CSV con estructuras: { "activity": "Grandmaster", "class": "Prismatic Hunter", "key\_exotic": "Spirit of Galanor", "mandatory\_mods": }.

### **2.2 El Motor de Inventario Personal (Contexto del Usuario)**

Esta es la pieza clave que diferencia tu app de ChatGPT.

* **Ingesta:** Tu app descarga el inventario del usuario (vía Bungie API) y lo filtra.  
* **Vectorización Ligera:** No vectorices *todo*. Crea un resumen JSON del usuario:  
  JSON  
  {  
    "exotics\_owned":,  
    "tier\_5\_armor\_count": 4,  
    "best\_weapons":,  
    "unlocked\_fragments": "ALL"  
  }

* Este JSON se pasa directo al contexto del LLM, no a la base vectorial, para asegurar que la IA *priorice* lo que el usuario realmente tiene.

## ---

**3\. Flujo de Trabajo de la IA (Pipeline)**

### **Paso 1: Interpretación de la Intención (Classifier)**

El usuario dice: *"Quiero una build para solear la nueva mazmorra con Titán Prismático y escopetas."*

* **LLM Router:** Un modelo pequeño/rápido (ej. GPT-3.5-Turbo o un modelo local cuantizado) clasifica la petición:  
  * *Actividad:* Dungeon Solo.  
  * *Restricción:* Prismatic Titan \+ Shotguns.  
  * *Objetivo:* Supervivencia \+ Burst DPS.

### **Paso 2: Búsqueda Semántica (Retrieval)**

El sistema busca en la Base de Conocimiento Vectorial (usando **Pinecone**, **ChromaDB** o **FAISS**):

* *Query:* "Best Prismatic Titan builds for solo survival shotgun synergy".  
* *Resultado:* Recupera fragmentos de texto sobre "Facet of Command" (recarga armas al congelar), "No Backup Plans" (exótico de escopetas) y builds meta actuales de Titán cuerpo a cuerpo.

### **Paso 3: Optimización Matemática (The Solver Link)**

**Aquí es donde integras tu investigación anterior.** El LLM es malo sumando números. No le pidas que calcule los stats.

* El LLM decide *qué* piezas buscar: "Necesitamos 100 Resiliencia y 100 Fuerza".  
* **Llamada a Función (Function Calling):** El LLM genera un objeto JSON de "Orden de Trabajo" para tu motor matemático (el código tipo D2ArmorPicker que investigamos).  
  JSON  
  {  
    "target\_stats": {"resilience": 100, "strength": 100},  
    "required\_exotic": "No Backup Plans",  
    "required\_mods":  
  }

* Tu motor matemático (en C++ o Rust/WASM para velocidad) ejecuta la permutación y devuelve: *"Es posible. Usa el Casco Tier 5 (id: 123\) y Guanteletes (id: 456). Mods necesarios: 3x."*

### **Paso 4: Generación de Respuesta (Synthesis)**

El LLM recibe el resultado matemático y redacta la respuesta final:  
"Aquí tienes tu build de 'Escopetero Inmortal'. He seleccionado tus No Backup Plans y configurado tus mods para tener 100 de Resiliencia. Usa el Aspecto 'Knockout' para curarte. Aquí está el link para equiparlo en un clic."

## ---

**4\. Tecnologías Recomendadas (Stack)**

* **Framework de IA:** **LangChain** (Python/JS). Es el estándar para unir LLMs con fuentes de datos. Facilita conectar la API de OpenAI/Anthropic con tu base de datos vectorial.  
* **Base de Datos Vectorial:** **ChromaDB** (Open Source y local). Ideal para empezar sin costes de nube. Almacena los "embeddings" del Manifiesto y las guías meta.  
* **LLM:**  
  * *Opción Premium:* **GPT-4o**. Mejor razonamiento para entender sinergias complejas de Destiny.  
  * *Opción Económica/Local:* **Llama-3-8B** (finetuneado). Puedes re-entrenar un modelo pequeño con miles de builds de Destiny para que entienda la jerga ("proc", "ad-clear", "dps phase").  
* **Backend:** **FastAPI** (Python). Para manejar las peticiones del frontend y orquestar el flujo RAG.

## **5\. Hoja de Ruta de Implementación**

1. **Fase de Datos (Data Engineering):** Escribe scripts para convertir el Manifiesto de Bungie en documentos de texto planos ("chunks") y cárgalos en ChromaDB.  
2. **Prototipo RAG:** Crea un script simple en Python que responda preguntas sobre perks ("¿Qué hace Facet of Ruin?") usando la base vectorial.  
3. **Integración de Inventario:** Conecta la autenticación de Bungie. Pasa el JSON del inventario del usuario al prompt del sistema.  
4. **Agente Híbrido:** Conecta la salida del LLM con tu algoritmo de optimización de stats (D2AP logic).

Esta arquitectura te coloca a la vanguardia. No solo filtras ítems; *entiendes* para qué sirven y construyes estrategias con ellos.