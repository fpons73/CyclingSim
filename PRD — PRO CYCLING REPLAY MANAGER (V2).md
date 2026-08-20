

**Versión:** 2.0  
**Plataforma:** Windows 11 (PC)  
**Género:** Simulador Deportivo / Estrategia por Turnos  
**Jugadores:** 1 — Gestión de uno o varios equipos / IA / Espectador  
**Motor Recomendado:** Godot 4.3+  
**Input:** Ratón, teclado, táctil opcional

---

# 1. Rol del Asistente (Meta Prompting)

Eres un desarrollador de software experto de clase mundial en Godot 4.3+, C#, UI Toolkit, SQLite y desarrollo de simuladores deportivos para Windows.

Tu objetivo es construir **Pro Cycling Replay Manager** con máxima precisión respecto a este documento.

La prioridad es:

1. Fidelidad a las reglas del sistema de juego de mesa en el que se basa.
    
2. Automatización de cálculos complejos.
    
3. Simulación coherente y reproducible.
    
4. Profundidad táctica basada en los atributos de los corredores.
    
5. Diferenciación clara entre corredores.
    
6. IA capaz de tomar decisiones utilizando exactamente el mismo motor que el jugador.
    
7. Interfaz clara y adecuada para Windows 11.
    

No inventes mecánicas que contradigan este documento.

Cuando una regla concreta del sistema original no esté especificada, debe implementarse mediante una arquitectura parametrizable para poder ajustarla posteriormente.

---

# 2. Descripción General y Visión

**Pro Cycling Replay Manager** es un simulador digital de ciclismo de carretera para Windows 11 basado en la experiencia de un juego de mesa de ciclismo tipo replay.

El jugador puede:

- dirigir uno o varios equipos;
    
- controlar directamente las decisiones tácticas;
    
- observar carreras completamente controladas por la IA;
    
- reproducir vueltas históricas;
    
- crear escenarios alternativos;
    
- jugar etapas individuales;
    
- disputar una Grande Vuelta completa.
    

La simulación debe conservar la tensión del sistema basado en dados, cartas, tablas y reglas del juego original, pero automatizando todos los cálculos.

## Objetivo principal

Crear un simulador donde una etapa pueda ser reproducida de principio a fin mediante:

**Datos de etapa + atributos de corredores + reglas de carrera + decisiones tácticas + RNG/dados + fatiga + situación de carrera**

El resultado debe generar carreras diferentes, pero estadísticamente coherentes.

---

# 3. Stack y Restricciones Técnicas

## Frontend / Motor

- Godot 4.3+.
    
- C#.
    
- UI Toolkit / UI Builder.
    
- Windows 11.
    
- Arquitectura modular.
    
- Separación estricta entre lógica de simulación y presentación visual.
    

## Base de datos

SQLite para:

- corredores;
    
- equipos;
    
- atributos;
    
- temporadas;
    
- resultados históricos;
    
- etapas;
    
- vueltas;
    
- clasificaciones;
    
- configuraciones.
    

## Datos de etapas

JSON/CSV para:

- perfil;
    
- distancia;
    
- secciones;
    
- puertos;
    
- pendientes;
    
- viento;
    
- adoquines;
    
- sprint;
    
- meta;
    
- modificadores;
    
- reglas especiales.
    

## Reproducibilidad

El sistema debe soportar una **seed de simulación**.

Con la misma:

- seed;
    
- etapa;
    
- corredores;
    
- atributos;
    
- configuración;
    

la simulación debe poder reproducirse exactamente.

---

# 4. Sistema de Corredores y Atributos

Cada corredor tendrá una ficha digital equivalente a una carta física, pero ampliada.

## Escala de atributos

Todos los atributos permanentes utilizan una escala de:

**50–99**

50 representa un nivel muy bajo dentro del universo de corredores disponibles.

99 representa un nivel excepcional.

Los atributos no deben interpretarse directamente como segundos, kilómetros ni porcentajes. El motor debe convertirlos en rendimiento mediante fórmulas y modificadores.

## Los 14 atributos

### 1. Llano

Capacidad del corredor para rendir en terreno llano.

Se utiliza principalmente en:

- etapas llanas;
    
- persecuciones;
    
- pelotón;
    
- fugas en terreno llano;
    
- viento cruzado;
    
- aproximaciones a meta.
    

### 2. Montaña

Capacidad en grandes puertos y alta montaña.

Se utiliza en:

- etapas de montaña;
    
- grandes ascensiones;
    
- ataques en puertos;
    
- pace checks;
    
- selección de grupos.
    

### 3. Media Montaña

Capacidad en:

- puertos intermedios;
    
- terreno quebrado;
    
- etapas de media montaña;
    
- sucesión de ascensiones no extremas.
    

### 4. Colina

Capacidad en:

- repechos;
    
- muros;
    
- subidas cortas;
    
- finales explosivos;
    
- ataques en terreno quebrado.
    

### 5. Crono

Capacidad en contrarrelojes individuales convencionales.

### 6. Prólogo

Capacidad en esfuerzos contra el reloj muy cortos.

### 7. Pavés

Especialización en adoquines.

Se utiliza en:

- sectores de pavés;
    
- etapas con adoquines;
    
- ataques sobre pavés;
    
- selección de grupos.
    

### 8. Sprint

Velocidad máxima en una llegada al sprint.

### 9. Aceleración

Capacidad para:

- arrancar;
    
- responder a ataques;
    
- cambiar de ritmo;
    
- abrir huecos;
    
- disputar finales explosivos.
    

Sprint y Aceleración son atributos independientes.

### 10. Descenso

Capacidad para descender.

Se utiliza en:

- finales en descenso;
    
- recuperación de tiempo bajando;
    
- ataques en descenso;
    
- selección de grupos.
    

### 11. Ataque (Escapadas)

Capacidad para:

- iniciar fugas;
    
- responder a ataques;
    
- crear diferencias;
    
- realizar movimientos ofensivos;
    
- ser efectivo dentro de una escapada.
    

No determina por sí solo el éxito de una fuga.

### 12. Aguante

Capacidad para mantener el rendimiento durante una etapa.

Reduce el deterioro provocado por:

- distancia;
    
- esfuerzos;
    
- ataques;
    
- persecuciones;
    
- ritmo elevado;
    
- etapas largas.
    

### 13. Resistencia

Capacidad para soportar esfuerzos acumulados.

Tiene especial importancia en:

- etapas largas;
    
- etapas duras;
    
- sucesión de etapas;
    
- Grandes Vueltas;
    
- acumulación de fatiga.
    

### 14. Recuperación

Capacidad para recuperar entre etapas.

Determina la cantidad de fatiga que el corredor conserva al comenzar la siguiente etapa.

---

# 5. Estados Dinámicos del Corredor

Los estados dinámicos no son atributos permanentes.

## Fatiga

Escala:

**0–100**

0 = completamente fresco.

100 = agotamiento extremo.

La fatiga aumenta por:

- kilómetros;
    
- desnivel;
    
- ritmo;
    
- ataques;
    
- persecuciones;
    
- esfuerzos para cerrar huecos;
    
- viento cruzado;
    
- pavés;
    
- incidentes;
    
- decisiones tácticas agresivas.
    

La fatiga reduce progresivamente el rendimiento efectivo.

El impacto de la fatiga debe estar modulado por:

- Aguante;
    
- Resistencia;
    
- situación de carrera.
    

## Fatiga entre etapas

Al terminar una etapa:

**Fatiga final → Recuperación → Fatiga residual**

La Recuperación del corredor determina cuánto se reduce la fatiga antes de la siguiente etapa.

La fatiga residual se conserva para el Tour.

---

# 6. Rendimiento Efectivo

Los atributos nunca deben utilizarse de forma aislada cuando la situación requiera varios factores.

El motor calculará un rendimiento efectivo utilizando:

**Atributo principal + atributos secundarios + situación + fatiga + RNG + reglas de etapa**

Ejemplo conceptual:

Un ataque en un puerto puede utilizar:

**Montaña + Ataque + Aceleración**

modificado por:

- Fatiga;
    
- posición;
    
- situación del grupo;
    
- objetivo táctico;
    
- RNG.
    

Un sprint masivo puede utilizar principalmente:

**Sprint + Aceleración**

Un tramo largo de llano puede utilizar:

**Llano + Aguante**

Un sector de pavés puede utilizar:

**Pavés + Llano + Aguante**

Las fórmulas concretas deben estar centralizadas en el motor de simulación y ser parametrizables.

---

# 7. Arquitectura de Simulación

La simulación continúa siendo **por secciones discretas**, no un juego de conducción en tiempo real.

Secuencia:

1. Cargar sección.
    
2. Identificar grupos.
    
3. Calcular estado de cada grupo.
    
4. Resolver incidentes.
    
5. Evaluar decisiones tácticas.
    
6. Resolver acciones.
    
7. Aplicar RNG/dados.
    
8. Calcular rendimiento.
    
9. Actualizar fatiga.
    
10. Actualizar tiempos.
    
11. Actualizar posiciones.
    
12. Actualizar clasificaciones.
    
13. Presentar resultado.
    
14. Solicitar decisiones al jugador si corresponde.
    
15. Pasar a la siguiente sección.
    

---

# 8. Sistema de Dados Virtual / RNG

Se mantiene el sistema de dados del PRD original.

## Dados

- 2d6 rojo/blanco.
    
- 1d10 azul.
    

Utilizados para:

- selección de equipos;
    
- incidentes;
    
- número de ataques;
    
- selección de corredores;
    
- checks de habilidad;
    
- resolución de situaciones;
    
- decimales/variación temporal cuando corresponda.
    

## Visualización

Opciones:

- dados 3D animados;
    
- resultado instantáneo;
    
- modo rápido.
    

El jugador podrá activar/desactivar la animación.

---

# 9. Tipos de Etapa

Se mantienen:

1. Flat Stage.
    
2. Flat Stage (Hilly).
    
3. Flat Stage (Cobbles).
    
4. Mountain Stage (Medium).
    
5. Mountain Stage.
    
6. Individual Time Trial.
    
7. Team Time Trial.
    
8. Crosswind Stage.
    
9. Prólogo.
    

Las etapas deben estar construidas mediante secciones.

Cada sección puede tener uno o varios tipos de terreno.

---

# 10. Etapas Llanas

Las etapas llanas mantienen la filosofía del sistema original:

- fuga temprana;
    
- persecución;
    
- pelotón activo/pasivo;
    
- incidentes;
    
- sprint final.
    

## Rendimiento

El atributo principal será:

**Llano**

Con influencia adicional de:

- Aguante;
    
- Ataque en fugas;
    
- Aceleración;
    
- Sprint en meta;
    
- Fatiga.
    

## Fuga

El motor seguirá calculando:

**Group Value (GV)**

y:

**Cohesion Level**

El GV de una fuga se calculará a partir del rendimiento efectivo de los corredores que forman el grupo, teniendo en cuenta el terreno y el estado de fatiga.

---

# 11. Etapas de Media Montaña y Colinas

Se introducen formalmente dos perfiles diferenciados.

## Media Montaña

Principal:

**Media Montaña**

Secundarios:

- Aguante;
    
- Ataque;
    
- Aceleración;
    
- Descenso.
    

## Colina

Principal:

**Colina**

Secundarios:

- Aceleración;
    
- Ataque;
    
- Aguante.
    

Estas etapas deben permitir finales donde un corredor que no sea escalador puro pueda marcar diferencias.

---

# 12. Etapas de Montaña

Se mantiene el sistema original de:

- selección de escaladores;
    
- secciones de puerto;
    
- Action Phase;
    
- ataques;
    
- Pace Checks;
    
- Counter-Attacks;
    
- Time Management;
    
- KoM.
    

## Ataques

El ataque de un corredor se resolverá teniendo en cuenta:

**Montaña + Ataque + Aceleración**

y será modificado por:

- fatiga;
    
- situación del grupo;
    
- posición;
    
- RNG.
    

## Pace Check

Para responder a un ataque se utilizará principalmente:

**Montaña**

con influencia de:

- Aguante;
    
- Fatiga;
    
- situación del corredor.
    

## Descenso

Cuando exista una sección de descenso:

**Descenso**

será el atributo principal.

También podrán intervenir:

- Aceleración;
    
- Fatiga;
    
- diferencia entre grupos.
    

---

# 13. Contrarreloj Individual

Se mantiene el procedimiento de dados del sistema original.

## CRI

Principal:

**Crono**

Secundarios:

- Aguante;
    
- Resistencia;
    
- Fatiga.
    

La distancia de la CRI determina cuánto peso tiene la capacidad de mantener el rendimiento.

---

# 14. Prólogo

Los prólogos utilizan:

**Prólogo**

como atributo principal.

Pueden intervenir:

- Aceleración;
    
- Fatiga;
    
- RNG.
    

El atributo Crono no debe sustituir automáticamente al atributo Prólogo.

---

# 15. Contrarreloj por Equipos

El TTT se resolverá a partir del rendimiento conjunto del equipo.

El sistema debe considerar:

- Crono;
    
- Llano;
    
- Aguante;
    
- Resistencia;
    
- composición del equipo.
    

Los corredores con menor rendimiento pueden afectar al rendimiento global según las reglas configuradas.

---

# 16. Viento Cruzado

Se mantiene la mecánica de:

- check previo;
    
- selección de equipos;
    
- formación de echelon;
    
- Echelon Value;
    
- segundo echelon;
    
- diferencias de tiempo.
    

Los atributos relevantes serán principalmente:

**Llano + Aguante**

con influencia de:

- Aceleración;
    
- Resistencia;
    
- Fatiga.
    

---

# 17. Adoquines

Se mantiene la estructura especial del PRD original.

Durante los sectores de pavés:

**Pavés**

será el atributo principal.

Podrán intervenir:

- Llano;
    
- Aguante;
    
- Aceleración;
    
- Ataque;
    
- Fatiga.
    

Los incidentes específicos de adoquines se mantienen.

---

# 18. Sprint

El sprint deja de depender exclusivamente de una única estadística.

## Sprint masivo

Principal:

**Sprint**

Secundario:

**Aceleración**

Modificadores:

- Fatiga;
    
- posición;
    
- situación de carrera;
    
- RNG.
    

## Sprint reducido

El peso de:

**Aceleración + Ataque + atributo del terreno**

puede aumentar.

## Final explosivo

Los finales con una llegada corta y explosiva utilizarán especialmente:

**Aceleración + Colina + Sprint**

según la configuración de la etapa.

---

# 19. Sistema de Fugas

El sistema de fugas continúa siendo uno de los pilares de la simulación.

## Inicio de fuga

El punto de ataque continúa determinado por el sistema de dados/reglas configurado.

## Selección

Los corredores serán seleccionados según:

- reglas de la etapa;
    
- comportamiento de los equipos;
    
- decisiones tácticas;
    
- IA o jugador.
    

## Rendimiento de la fuga

El rendimiento dependerá de:

- Ataque;
    
- atributo del terreno;
    
- Llano/Montaña/Media Montaña/Colina/Pavés;
    
- Aguante;
    
- Resistencia;
    
- Fatiga.
    

El GV se recalculará cuando sea necesario.

---

# 20. Pelotón

Se mantiene:

- Pelotón activo.
    
- Pelotón pasivo.
    
- Tempo.
    
- Modificadores.
    
- Diferencias de tiempo.
    

El rendimiento del pelotón se calculará a partir de los corredores que efectivamente estén realizando el esfuerzo.

El motor no debe considerar todos los corredores del grupo como igualmente activos.

---

# 21. IA Táctica

La IA utiliza exactamente los mismos atributos y reglas que el jugador.

No existe un sistema de estadísticas paralelo para la IA.

La IA evalúa:

- atributos del corredor;
    
- fatiga;
    
- posición;
    
- clasificación general;
    
- diferencia temporal;
    
- compañeros;
    
- fuga;
    
- distancia restante;
    
- tipo de terreno;
    
- situación del pelotón;
    
- objetivo del equipo;
    
- importancia de la etapa.
    

## Decisiones

La IA puede decidir:

- atacar;
    
- seguir un ataque;
    
- perseguir;
    
- mantener ritmo;
    
- entrar en fuga;
    
- proteger líder;
    
- ahorrar energía;
    
- lanzar sprint;
    
- disputar KoM;
    
- controlar pelotón;
    
- realizar contraataque;
    
- asumir riesgos en descenso.
    

La calidad de la decisión debe depender de la lógica de IA, no modificar artificialmente los atributos del corredor.

---

# 22. Control del Jugador

Cuando el jugador dirige un equipo, las decisiones tácticas de sus corredores se presentan mediante la interfaz.

La simulación se pausa o entra en estado de decisión.

Ejemplo:

**KM 164 — Ataque detectado**

> Cancellara ataca.

Opciones:

- Responder.
    
- Atacar.
    
- Mantener ritmo.
    
- No responder.
    

La disponibilidad de acciones dependerá de:

- situación;
    
- tipo de corredor;
    
- fatiga;
    
- reglas;
    
- posición;
    
- etapa.
    

Una vez tomada la decisión, el motor continúa.

---

# 23. Modo Espectador

En Director Mode:

- todos los equipos son IA;
    
- el jugador no toma decisiones;
    
- el motor ejecuta las decisiones automáticamente.
    

El jugador puede:

- pausar;
    
- acelerar;
    
- ralentizar;
    
- avanzar sección;
    
- consultar estadísticas;
    
- revisar decisiones;
    
- observar la carrera.
    

Debe ser posible utilizar este modo para streaming y análisis.

---

# 24. Equipos y Roles

Los equipos tendrán:

- líder;
    
- corredores protegidos;
    
- gregarios;
    
- sprinters;
    
- escaladores;
    
- especialistas;
    
- corredores de fuga.
    

Las etiquetas de especialización no sustituyen los 14 atributos.

Deben servir principalmente para:

- comportamiento IA;
    
- presentación;
    
- selección;
    
- estrategia.
    

Un corredor puede pertenecer a varias categorías.

---

# 25. Clasificaciones

Se mantienen:

### General

Suma acumulada de tiempos.

### Puntos

Puntos de metas y sprints intermedios según configuración.

### Montaña

Puntos de puertos según categoría.

### Jóvenes

Mejor corredor sub-25.

### Equipos

Suma de los tres mejores corredores por etapa.

---

# 26. Gestión de una Grande Vuelta

En el modo Tour:

- los corredores conservan fatiga;
    
- Recuperación actúa entre etapas;
    
- Resistencia influye en la acumulación;
    
- el rendimiento puede degradarse con el paso de las etapas.
    

La composición de la plantilla será relevante porque un equipo con buenos especialistas puede gestionar mejor diferentes tipos de etapas.

---

# 27. Base de Datos de Corredores

Cada corredor almacenará:

## Identidad

- Nombre.
    
- Nacionalidad.
    
- Edad.
    
- Equipo.
    
- Número.
    
- Especializaciones.
    

## 14 atributos

- Llano.
    
- Montaña.
    
- Media Montaña.
    
- Colina.
    
- Crono.
    
- Prólogo.
    
- Pavés.
    
- Sprint.
    
- Aceleración.
    
- Descenso.
    
- Ataque.
    
- Aguante.
    
- Resistencia.
    
- Recuperación.
    

## Estado

- Activo.
    
- Questionable.
    
- Dropped.
    
- DNS.
    
- DNF.
    
- DSQ/Penalizado.
    

## Estado dinámico

- Fatiga actual.
    
- Tiempo acumulado.
    
- Posición.
    
- Grupo actual.
    

---

# 28. Stage Replay Data

Los archivos JSON/CSV deben poder describir:

- nombre;
    
- fecha;
    
- distancia;
    
- tipo;
    
- secciones;
    
- terreno de cada sección;
    
- Tempo Modifier;
    
- incidentes;
    
- puertos;
    
- categoría;
    
- longitud;
    
- pendiente;
    
- descenso;
    
- adoquines;
    
- viento;
    
- sprint intermedio;
    
- meta;
    
- reglas especiales;
    
- Time Factor;
    
- Winner's Time.
    

El sistema debe permitir modificar estos datos sin necesidad de recompilar el juego.

---

# 29. Interfaz de Usuario

## Pantalla principal

Estilo:

**sports manager + simulador de carrera**

Debe priorizar:

- información;
    
- claridad;
    
- lectura rápida;
    
- mapas/perfiles;
    
- grupos;
    
- tiempos.
    

## Carrera

Elementos:

- perfil de etapa;
    
- km actual;
    
- km restantes;
    
- grupos;
    
- gaps;
    
- clasificación;
    
- log de acciones;
    
- fatiga;
    
- estado de corredores;
    
- decisiones.
    

## Ficha del corredor

Mostrar los 14 atributos claramente.

Los atributos deberán visualizarse en escala 50–99.

Ejemplo:

**Montaña 94**  
**Media Montaña 89**  
**Colina 83**

También mostrar:

**Fatiga 62%**

pero diferenciándola visualmente de los atributos permanentes.

---

# 30. Pantalla Pre-Etapa

Mostrar:

- recorrido;
    
- perfil;
    
- distancia;
    
- características;
    
- favoritos;
    
- corredores;
    
- DNS;
    
- Questionable;
    
- estado de fatiga;
    
- estrategia del equipo.
    

En modo Tour debe mostrar también:

**Fatiga heredada de la etapa anterior.**

---

# 31. Pantalla Post-Etapa

Mostrar:

- ganador;
    
- clasificación completa;
    
- gaps;
    
- cambios en CG;
    
- Puntos;
    
- KoM;
    
- Jóvenes;
    
- Equipos;
    
- incidentes;
    
- abandonos;
    
- fatiga final;
    
- recuperación aplicada;
    
- resumen táctico.
    

---

# 32. Guardado

Debe existir:

- guardado durante etapa;
    
- guardado después de etapa;
    
- guardado de Tour;
    
- múltiples partidas;
    
- seed de simulación.
    

El estado debe poder recuperarse exactamente.

---

# 33. Exportación

Permitir:

- CSV.
    
- HTML.
    

Exportar:

- resultados;
    
- clasificaciones;
    
- tiempos;
    
- corredores;
    
- estadísticas;
    
- eventos principales.
    

---

# 34. Modos de Juego

## Historia / Replay Histórico

Reproduce vueltas históricas.

Ejemplo:

**Tour de France 2010**

Los corredores y equipos se cargan desde los datos históricos disponibles.

## Tour Mode

- 21 etapas + prólogo si la configuración lo permite.
    
- Plantilla.
    
- Fatiga.
    
- Recuperación.
    
- Clasificaciones acumuladas.
    

## Etapa Individual / Sandbox

Permite configurar:

- etapa;
    
- corredores;
    
- equipos;
    
- seed;
    
- condiciones;
    
- reglas.
    

## Director Mode

Todos los equipos controlados por IA.

---

# 35. Dificultad

## Strict

Reproduce las reglas del sistema original con mínima intervención.

## Manager

Permite al jugador tomar decisiones tácticas adicionales mientras la aplicación automatiza los cálculos.

---

# 36. Accesibilidad

- Tema claro.
    
- Tema oscuro.
    
- Compatibilidad con escalado DPI.
    
- Resoluciones 1080p, 1440p y 4K.
    
- Daltonismo.
    
- Los dados no deben depender únicamente del color.
    
- Atajos de teclado.
    
- Velocidad de simulación configurable.
    

---

# 37. Windows 11

Debe soportar:

- ventana;
    
- pantalla completa;
    
- redimensionado;
    
- Snap Layouts;
    
- escalado DPI;
    
- diferentes relaciones de aspecto;
    
- teclado y ratón.
    

---

# 38. Localización

Idiomas iniciales:

- Español.
    
- Inglés.
    
- Francés.
    

Todos los textos deben estar preparados para localización.

---

# 39. Arquitectura de Software

Separar:

### Simulation Core

Contiene:

- reglas;
    
- dados;
    
- RNG;
    
- atributos;
    
- fatiga;
    
- grupos;
    
- tiempos;
    
- IA;
    
- clasificaciones.
    

### Presentation Layer

Contiene:

- UI;
    
- animaciones;
    
- dados;
    
- visualización de grupos;
    
- perfiles;
    
- log.
    

### Data Layer

Contiene:

- SQLite;
    
- JSON;
    
- CSV;
    
- partidas guardadas.
    

La lógica de simulación no debe depender de la interfaz gráfica.

---

# 40. Fórmulas y Motor de Rendimiento

No codificar fórmulas directamente dentro de componentes de UI.

Crear servicios especializados:

- `RiderPerformanceCalculator`
    
- `FatigueCalculator`
    
- `GroupValueCalculator`
    
- `BreakawayCalculator`
    
- `SprintCalculator`
    
- `TimeTrialCalculator`
    
- `MountainCalculator`
    
- `CrosswindCalculator`
    
- `CobblesCalculator`
    
- `RecoveryCalculator`
    
- `RaceDecisionEngine`
    

Los modificadores deben ser configurables.

El sistema debe permitir ajustar el peso de cada atributo durante el desarrollo sin tener que reescribir todo el motor.

---

# 41. Principio Fundamental del Sistema de Stats

Los 14 atributos **no sustituyen las reglas de la carrera**.

Son los valores que permiten determinar cómo responde cada corredor ante esas reglas.

La estructura general será:

**Reglas de etapa**

↓

**Situación de carrera**

↓

**Atributos relevantes del corredor**

↓

**Fatiga / resistencia / estado**

↓

**Decisión táctica**

↓

**RNG / dados**

↓

**Rendimiento efectivo**

↓

**Resultado**

Esto debe mantenerse como principio fundamental de la arquitectura.

---

# 42. Roadmap

## Fase 1 — MVP

- Motor RNG.
    
- Base de datos.
    
- 14 atributos.
    
- Ficha de corredor.
    
- Fatiga.
    
- Etapas llanas.
    
- Fugas.
    
- Pelotón.
    
- Sprint.
    
- IA básica.
    
- UI de simulación.
    
- Una etapa histórica.
    

## Fase 2 — Montaña y Tour

- Media Montaña.
    
- Colina.
    
- Montaña.
    
- Descensos.
    
- KoM.
    
- Recuperación.
    
- Resistencia.
    
- Tour completo.
    
- Clasificaciones.
    

## Fase 3 — Especialidades

- CRI.
    
- Prólogo.
    
- TTT.
    
- Pavés.
    
- Viento cruzado.
    
- IA avanzada.
    

## Fase 4 — Pulido

- Dados 3D.
    
- Animaciones.
    
- Modo espectador avanzado.
    
- Editor de etapas.
    
- Importación histórica.
    
- Optimización Windows 11.
    
- Localización.
    

---

# 43. Métricas de Éxito

- 100% de las reglas implementadas correctamente.
    
- Resultados reproducibles mediante seed.
    
- Etapas llanas: aproximadamente 10–15 minutos.
    
- Montaña: aproximadamente 20–25 minutos.
    
- Tour completo sin errores de estado.
    
- Diferenciación estadística clara entre corredores.
    
- IA capaz de producir estrategias coherentes.
    
- El jugador debe poder entender por qué un corredor ha rendido bien o mal.
    

---

# 44. Scope

## Incluido

- Simulador de ciclismo.
    
- Windows 11.
    
- Dados/RNG.
    
- Reglas de replay.
    
- 14 atributos 50–99.
    
- Fatiga.
    
- Aguante.
    
- Resistencia.
    
- Recuperación.
    
- Fugas.
    
- Pelotón.
    
- Sprint.
    
- Montaña.
    
- Media Montaña.
    
- Colina.
    
- Descenso.
    
- Pavés.
    
- CRI.
    
- Prólogo.
    
- TTT.
    
- Viento cruzado.
    
- IA.
    
- Control manual.
    
- Director Mode.
    
- Tour Mode.
    
- Replay histórico.
    
- Sandbox.
    
- SQLite.
    
- JSON/CSV.
    
- Guardado.
    
- Exportación.
    
- Localización.
    

## Excluido inicialmente

- Multijugador online.
    
- Mercado de fichajes avanzado.
    
- Economía completa de equipos.
    
- Patrocinadores.
    
- Entrenamiento detallado de corredores.
    
- Gestión financiera.
    
- Animación 3D realista de ciclistas.
    
- Física de conducción manual.
    
- Mundo abierto.
    

Estas características podrán evaluarse para futuras versiones.

---

# 45. Nota Final (Chat Mode)

Antes de generar código, el modelo debe leer este documento y confirmar su entendimiento en modo conversación (Chat Mode).

Debe comprobar especialmente:

1. Los 14 atributos utilizan escala **50–99**.
    
2. Los atributos son independientes.
    
3. Fatiga, posición y estado son variables dinámicas.
    
4. Aguante, Resistencia y Recuperación tienen funciones diferentes.
    
5. El motor de simulación es común para jugador e IA.
    
6. El jugador decide cuando controla el equipo.
    
7. La IA decide cuando controla el equipo.
    
8. En modo espectador todos los equipos utilizan IA.
    
9. Las reglas de _La Grande Boucle_ continúan siendo el núcleo del sistema.
    
10. Los atributos alimentan las reglas, no las sustituyen.
    
11. El RNG y las semillas deben permitir simulaciones reproducibles.
    
12. Las fórmulas deben estar centralizadas y ser parametrizables.