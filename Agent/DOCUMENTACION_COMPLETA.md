# Documentacion Completa - Tutor de Idiomas

Stack: .NET 8 + PostgreSQL + Entity Framework Core + Groq AI API + React + TypeScript + Tailwind

---

## Indice

1. [Stack Tecnologico](#1-stack-tecnologico)
2. [Arquitectura](#2-arquitectura)
3. [Base de Datos](#3-base-de-datos)
4. [Componentes Reutilizables](#4-componentes-reutilizables)
5. [Tecnicas de Refactorizacion](#5-tecnicas-de-refactorizacion)
6. [Manejo de Errores](#6-manejo-de-errores)
7. [Endpoints de la API](#7-endpoints-de-la-api)
8. [Autenticacion](#8-autenticacion)
9. [Setup / Inicializacion](#9-setup--inicializacion)
10. [Flujo de Pronunciacion](#10-flujo-de-pronunciacion)
11. [Ejercicios](#11-ejercicios)
12. [Progreso y Gamificacion](#12-progreso-y-gamificacion)
13. [Frontend](#13-frontend)
14. [Bugs Corregidos](#14-bugs-corregidos)
15. [Estructura del Proyecto](#15-estructura-del-proyecto)
16. [Credenciales de Prueba](#16-credenciales-de-prueba)

---

## 1. Stack Tecnologico

| Tecnologia | Uso | Estado |
|------------|-----|--------|
| React 19 + TypeScript | Frontend - Componentes reutilizables | |
| ASP.NET Core 8 (Web API) | Backend - Servicios y controladores | |
| PostgreSQL (puerto 5433) | Base de datos | |
| Groq API (Whisper-large-v3) | Voz a texto para pronunciacion | |
| Groq API (Llama 3.3-70b) | Correccion gramatical y feedback adaptativo | |
| Tailwind CSS v4 | Estilos del frontend | |
| React Query | Cache y sincronizacion de datos | |
| React Router v6 | Routing del frontend | |

---

## 2. Arquitectura

### Capas del Backend

| Capa | Ubicacion | Descripcion |
|------|-----------|-------------|
| **Presentacion** | `Controllers/` | 9 controladores con endpoints REST |
| **Servicios** | `Services/` | Logica de negocio y comunicacion con IA |
| **Datos** | `Repositories/` + `Data/BdContext.cs` | Acceso a PostgreSQL via EF Core |

### Diagrama

```
React App (Frontend)
      |
      | HTTP (JWT Auth)
      v
ASP.NET Core Web API ------------------------+
  |                                           |
  |- Controllers/   (Routing + Auth)          |
  |- Services/      (Logica de IA) ---------> Groq API
  |- Repositories/  (Acceso a datos)          |  |- Whisper (audio->texto)
  |- Data/BdContext (EF Core)                 |  |- Llama  (feedback)
         |                                     |
         v                                     |
    PostgreSQL                                 |
  (Users, Languages, Lessons,                  |
   Exercises, Progress,                        |
   PronunciationAttempts)                      |
                                                |
         <-------------------------------------+
```

### Configuracion

- **Backend:** `http://localhost:14567`
- **Frontend:** `http://localhost:5173` (con proxy a backend via Vite)
- **BD:** PostgreSQL puerto 5433, Database: `TutorIdiomasDB`
- Migraciones automaticas al iniciar
- CORS multi-origen
- JsonOptions con `ReferenceHandler.IgnoreCycles`

---

## 3. Base de Datos

### Tablas

| Tabla | Columnas |
|-------|----------|
| **Users** | Id (Guid), Name (varchar 100), Email (varchar 200), PasswordHash, Role, CreatedAt |
| **Languages** | Id, Name, Code (ISO), FlagIcon |
| **Lessons** | Id, LanguageId (FK), Title, Level, XpReward |
| **Exercises** | Id, LessonId (FK), Type (pronunciation/translation/grammar), Content (JSONB) |
| **UserProgress** | Id, UserId (FK), LanguageId (FK), LessonId (FK), Score, Completed, CompletedAt |
| **PronunciationAttempts** | Id, UserId (FK), ExerciseId (FK), AudioUrl, RecognizedText, ExpectedText, Score, CreatedAt |
| **RefreshTokens** | Id, Token, Expires, IsRevoked, UserId (FK) |

### Migraciones

- `20260519112930_p1.cs` - Migracion inicial
- `20260519221653_AddUserNombre.cs` - Agrega columna `Name` a `Users`

---

## 4. Componentes Reutilizables

### Componente 1: ChatbotConnectorService (Backend)

**Archivo:** `Services/GroqAiService.cs`

**Proposito:** Clase generica que recibe un Prompt y un Modelo y devuelve la respuesta de la IA.

```csharp
// Metodo generico - acepta cualquier prompt y cualquier modelo
public async Task<string> QueryAiAsync(string prompt, string model)
{
    var requestBody = new
    {
        model,  // Parametro: "llama-3.3-70b-versatile", "mixtral-8x7b", etc.
        messages = new[] { new { role = "user", content = prompt } },
        temperature = 0.7,
        max_tokens = 500
    };
    // ... llamada HTTP a Groq API
}

// Metodo de conveniencia para Llama (usa el modelo por defecto del config)
public async Task<string> QueryLlamaAsync(string prompt)
    => await QueryAiAsync(prompt, _groqSettings.LlamaModel);
```

**Reutilizacion:** El mismo metodo sirve para corregir gramatica, traducir, generar ejercicios nuevos, o incluso para un futuro "Tutor de Matematicas" - solo cambia el prompt y el modelo.

### Componente 2: ExerciseRenderer (Frontend)

**Archivo:** `src/components/exercises/ExerciseRenderer.tsx`

Componente de React que recibe un `Exercise` con tipo y contenido JSON y renderiza el componente adecuado:
- `translation` -> `TranslationExercise.tsx`
- `grammar` -> `GrammarExercise.tsx`
- `pronunciation` -> `PronunciationExercise.tsx`

### Componente 3: useAudio (Frontend)

**Archivo:** `src/hooks/useAudio.ts`

Hook de React para capturar audio del microfono con metodos `startRecording()`, `stopRecording()`, estados `isRecording`, `audioBase64`, y limpieza automatica al desmontar.

---

## 5. Tecnicas de Refactorizacion

### Tecnica 1: Extract Method

**Problema:** `ExercisesController.SubmitAnswer()` tenia validacion, scoring y guardado en BD todo en un solo metodo (~80 lineas).

**Antes:**
```csharp
// Logica de scoring inline en el controller
if (exercise.Type == "translation")
{
    // decodificar JSON, calcular score, generar feedback...
}
else if (exercise.Type == "grammar") { /* ... */ }
else if (exercise.Type == "pronunciation") { /* ... */ }
```

**Despues:**
```csharp
// ExerciseScoringService.cs - Servicio independiente
public class ExerciseScoringService
{
    public ExerciseScoreResult EvaluateTranslation(string userAnswer, string exerciseContent) { ... }
    public ExerciseScoreResult EvaluateGrammar(string userAnswer, string exerciseContent) { ... }
    public ExerciseScoreResult EvaluatePronunciation(string userAnswer, string? expectedPhrase) { ... }
}

// ExercisesController.cs - Solo orquestacion
var result = exercise.Type switch
{
    "translation"   => _scoringService.EvaluateTranslation(request.UserAnswer, exercise.Content),
    "grammar"       => _scoringService.EvaluateGrammar(request.UserAnswer, exercise.Content),
    "pronunciation" => _scoringService.EvaluatePronunciation(request.UserAnswer, request.ExpectedPhrase),
    _               => throw new ArgumentException($"Tipo desconocido: {exercise.Type}")
};
```

**Beneficio:** Controller limpio, logica testeable y reutilizable.

### Tecnica 2: Replace Magic Number with Symbolic Constant

**Antes:**
```csharp
new Lesson { Id = 1, LanguageId = 1, Title = "Saludos" },
new Lesson { Id = 4, LanguageId = 2, Title = "Saludos en Espanol" },
```

**Despues:**
```csharp
// Models/LanguageConstants.cs
public static class LanguageConstants
{
    public const int Ingles   = 1;
    public const int Espanol  = 2;
    public const int Frances  = 3;
    public const int Aleman   = 4;
    public const int Italiano = 5;
}

new Lesson { Id = 1, LanguageId = LanguageConstants.Ingles, Title = "Saludos" },
new Lesson { Id = 4, LanguageId = LanguageConstants.Espanol, Title = "Saludos en Espanol" },
```

**Beneficio:** Codigo autodocumentado. Si cambia un ID, se modifica en un solo lugar.

---

## 6. Manejo de Errores

| Mecanismo | Archivo | Cobertura |
|-----------|---------|-----------|
| Middleware global | `Middleware/ExceptionMiddleware.cs` | Captura y formatea toda excepcion no manejada (401, 404, 409, 500, 502, 504) |
| Filtro de validacion | `Filters/ValidationFilter.cs` | Valida automaticamente DataAnnotations en todos los DTOs |
| try-catch especificos | `Controllers/*.cs` | Cada endpoint maneja FormatException, HttpRequestException, UnauthorizedAccess, ArgumentException |
| Timeouts IA | `Services/GroqAiService.cs` | Timeout de 30s para Llama, 60s para Whisper |

---

## 7. Endpoints de la API

| Metodo | Ruta | Auth | Descripcion |
|--------|------|------|-------------|
| POST | `/api/auth/register` | No | Registro de usuario |
| POST | `/api/auth/login` | No | Login (devuelve JWT) |
| POST | `/api/auth/refresh` | No | Renovar tokens |
| POST | `/api/auth/revoke` | Si | Revocar token |
| GET | `/api/auth/me` | Si | Usuario actual |
| GET | `/api/languages` | Si | Lista de idiomas |
| GET | `/api/languages/{id}/lessons` | Si | Lecciones por idioma |
| GET | `/api/lessons/language/{id}` | Si | Lecciones con progreso |
| GET | `/api/lessons/{id}` | Si | Leccion por ID |
| GET | `/api/exercises/lesson/{id}` | Si | Ejercicios por leccion |
| GET | `/api/exercises/{id}` | Si | Ejercicio por ID |
| POST | `/api/exercises/{id}/submit` | Si | Enviar respuesta |
| GET | `/api/exercises/{id}/hint` | Si | Obtener pista |
| POST | `/api/pronunciation/evaluate` | Si | Evaluar pronunciacion (audio Base64 -> Whisper -> feedback) |
| POST | `/api/pronunciation/practice-word` | Si | Practicar palabra |
| GET | `/api/pronunciation/history` | Si | Historial de intentos |
| GET | `/api/pronunciation/history/{exerciseId}` | Si | Intentos por ejercicio |
| GET | `/api/pronunciation/stats` | Si | Estadisticas |
| GET | `/api/pronunciation/difficult-words` | Si | Palabras con bajo score |
| POST | `/api/grammar/correct` | Si | Correccion gramatical via Groq |
| GET | `/api/progress/me` | Si | Progreso del usuario (nivel, XP, racha) |
| GET | `/api/progress/me/language/{id}` | Si | Progreso por idioma |
| POST | `/api/progress/lesson/{id}/complete` | Si | Completar leccion (retorna siguiente leccion) |
| GET | `/api/progress/streak` | Si | Racha de dias consecutivos |
| GET | `/api/progress/leaderboard` | Si | Ranking de usuarios por XP |
| GET | `/api/user/me` | Si | Obtener perfil |
| PUT | `/api/user/me` | Si | Actualizar perfil (nombre) |
| PUT | `/api/user/change-password` | Si | Cambiar contrasena |
| POST | `/api/setup/inicializar-sistema` | No | Inicializar BD con datos de prueba |
| POST | `/api/setup/llenar-ejercicios` | No | Agregar contenido faltante |
| GET | `/api/setup/verificar-estado` | No | Ver conteo de registros |

---

## 8. Autenticacion

- **AccessToken:** JWT de 60 minutos
- **RefreshToken:** Token opaco de 7 dias almacenado en BD
- Flujo de refresh implementado en frontend con cola para evitar race conditions en 401 concurrentes
- Roles: `Admin`, `User`

---

## 9. Setup / Inicializacion

### Endpoints

1. **`POST /api/setup/inicializar-sistema`**
   - Crea 5 idiomas (Ingles, Espanol, Frances, Aleman, Italiano)
   - Crea 15 lecciones (3 por idioma)
   - Crea 53 ejercicios
   - Crea usuario admin (`admin@tutor.com` / `Admin123`)
   - Crea usuario test (`test@test.com` / `123456`)

2. **`POST /api/setup/llenar-ejercicios`**
   - Endpoint adicional por si faltan lecciones
   - Agrega solo el contenido que no exista en BD

3. **`GET /api/setup/verificar-estado`**
   - Devuelve conteo de registros en cada tabla

### Lecciones (15 total, 3 por idioma)

| ID | Idioma | Titulo | Nivel | XP |
|----|--------|--------|-------|----|
| 1 | Ingles | Saludos y Presentaciones | 1 | 50 |
| 2 | Ingles | Numeros y Contar | 1 | 50 |
| 3 | Ingles | Familia y Amigos | 1 | 60 |
| 4 | Espanol | Saludos en Espanol | 1 | 50 |
| 8 | Espanol | Vocabulario Basico | 1 | 50 |
| 9 | Espanol | Verbos Esenciales | 1 | 50 |
| 5 | Frances | Bonjour! Saludos | 1 | 50 |
| 10 | Frances | Vocabulaire de Base | 1 | 50 |
| 11 | Frances | Verbes Essentiels | 1 | 50 |
| 6 | Aleman | Hallo! Saludos en Aleman | 1 | 50 |
| 12 | Aleman | Grundwortschatz | 1 | 50 |
| 13 | Aleman | Wichtige Verben | 1 | 50 |
| 7 | Italiano | Ciao! Saludos en Italiano | 1 | 50 |
| 14 | Italiano | Vocabolario di Base | 1 | 50 |
| 15 | Italiano | Verbi Essenziali | 1 | 50 |

---

## 10. Flujo de Pronunciacion

```
Frontend (JS)                           Backend (C#)
     |                                      |
     |  1. Graba audio (MediaRecorder)       |
     |  2. Convierte a Base64                |
     |  POST /api/pronunciation/evaluate     |
     |  { audioBase64, expectedPhrase,       |
     |    exerciseId }                       |
     |------------------------------------->|
     |                                      |
     |                    3. Decodifica Base64 -> byte[]
     |                    4. Envia a Groq Whisper API
     |                       (audio/webm, whisper-large-v3)
     |                    5. Whisper devuelve texto reconocido
     |                    6. Envia a Groq Llama para feedback
     |                    7. Calcula score con Levenshtein
     |                    8. Guarda PronunciationAttempt en BD
     |                                      |
     |  <-----------------------------------|
     |  { recognizedText, score,             |
     |    grammarFeedback, suggestions }      |
```

**Detalles tecnicos:**
- Llega como Base64 desde el frontend
- Se convierte a `byte[]` y se envia a Groq como `audio/webm`
- NO se guarda el archivo de audio, solo el texto reconocido
- Puntuacion: algoritmo Levenshtein (distancia de edicion)

---

## 11. Ejercicios

`POST /api/exercises/{id}/submit`

```json
{ "userAnswer": "...", "expectedPhrase": "...", "timeSpentSeconds": 0 }
```

Segun el tipo:
- **translation** - Compara palabras clave, score parcial
- **grammar** - Coincidencia exacta (0 o 100) u opcion multiple
- **pronunciation** - Similaridad caracter por caracter

Cada leccion tiene 3 ejercicios (uno de cada tipo).

---

## 12. Progreso y Gamificacion

- **Nivel:** `(XP total / 500) + 1`
- **XP:** Se otorga solo en la primera vez que se completa una leccion
- **Racha:** Dias consecutivos con actividad
- **Leaderboard:** Ranking de usuarios por XP total (ordenado correctamente)

---

## 13. Frontend

### Tecnologias
- React 19 + TypeScript
- React Router v6
- React Query (@tanstack/react-query)
- Tailwind CSS v4
- Axios para HTTP
- Vite como bundler

### Tema
- **Paleta:** Purple (primario) + Amber (acento)
- **Fuente:** Poppins
- **Sin emojis** - iconos SVG personalizados en sidebar
- Diseno responsivo

### Paginas
- `/login` - Inicio de sesion
- `/register` - Registro
- `/dashboard` - Panel principal con progreso
- `/languages` - Seleccion de idioma
- `/languages/:id/lessons` - Lista de lecciones por idioma
- `/lessons/:id` - Detalle de leccion (ejercicios)
- `/leaderboard` - Ranking de usuarios
- `/profile` - Perfil y cambio de contrasena

### Componentes de Ejercicios
- `TranslationExercise.tsx` - Traduccion con input de texto
- `GrammarExercise.tsx` - Opcion multiple o texto libre
- `PronunciationExercise.tsx` - Grabacion de audio + evaluacion IA
- `ExerciseRenderer.tsx` - Delegador que renderiza el tipo correcto

### UI Componentes
- `Button.tsx` - Boton con variantes y loading state
- `Card.tsx` - Contenedor con sombra
- `Input.tsx` - Input con forwardRef
- `Modal.tsx` - Modal con backdrop click

---

## 14. Bugs Corregidos

### Backend

| Bug | Archivo | Solucion |
|-----|---------|----------|
| Leaderboard muestra usuarios incorrectos (Toma sin ordenar) | `ProgressRepository.cs` | Move `Take(limit)` despues de `OrderByDescending` |
| UpdateProfile no guarda el nombre | `UserController.cs` | Agregar `user.Name = request.Name` |
| ExtractHint se cae con ejercicios de gramatica (JSON con arrays) | `ExercisesController.cs` | Usar `JsonDocument.Parse` con `TryGetProperty` en vez de `Dictionary<string,string>` |
| XP se duplica o da 0 segun el dia | `ProgressController.cs` | XP solo en primera completion (verificar `CompletedAt` anterior a hoy) |
| GetUserId lanza FormatException con token invalido | `ProgressController.cs` | Envolver `Guid.Parse` en try/catch |

### Frontend

| Bug | Archivo | Solucion |
|-----|---------|----------|
| Al navegar entre lecciones el estado persiste (se ve pantalla de completado de la anterior) | `LessonDetailPage.tsx` | Agregar `useEffect` con `lessonId` que resetea todo el estado local |
| Microfono sigue activo al salir del componente | `useAudio.ts` | Agregar `useEffect` cleanup que detiene el `MediaRecorder` al desmontar |
| Login exitoso pero redirect a /login si fetchUser falla | `AuthContext.tsx` | `fetchUser` ahora propaga errores, catch en useEffect limpia tokens |
| Race condition en refresh de token (multiples 401 simultaneos) | `client.ts` | Implementar cola de espera con `isRefreshing` + `failedQueue` |
| onFeedbackClose undefined crashea ejercicios | `TranslationExercise.tsx`, `GrammarExercise.tsx`, `PronunciationExercise.tsx` | Cambiar `onClick={onFeedbackClose}` por `onClick={() => onFeedbackClose?.()}` |
| Non-null assertion en `content.options!` | `GrammarExercise.tsx` | Cambiar a `content.options?.map()` |
| NaN en languageId de ruta | `LessonsPage.tsx` | Agregar guard `isNaN(langId)` |
| `completed: false` saltaba el lookup de progreso | `LessonsPage.tsx` | `l.completed ??` -> `l.completed ||` |

---

## 15. Estructura del Proyecto

### Backend (`traductorPao/`)

```
Api_TutorIdiomas/
+-- Agent/
|   +-- CHECKPOINT_CONSIGNA.md
|   +-- DOCUMENTACION_BACKEND.md
|   +-- DOCUMENTACION_COMPLETA.md        <-- Este archivo
+-- Controllers/
|   +-- AuthController.cs
|   +-- ExercisesController.cs
|   +-- GrammarController.cs
|   +-- LanguagesController.cs
|   +-- LessonsController.cs
|   +-- ProgressController.cs
|   +-- PronunciationController.cs
|   +-- SetupController.cs
|   +-- UserController.cs
+-- Data/
|   +-- BdContext.cs
+-- Filters/
|   +-- ValidationFilter.cs
+-- Middleware/
|   +-- ExceptionMiddleware.cs
+-- Migrations/
|   +-- 20260519112930_p1.cs
|   +-- 20260519112930_p1.Designer.cs
|   +-- 20260519221653_AddUserNombre.cs
|   +-- 20260519221653_AddUserNombre.Designer.cs
|   +-- BdContextModelSnapshot.cs
+-- Models/
|   +-- DTOs/
|   |   +-- AuthResponse.cs
|   |   +-- ExerciseSubmitDto.cs
|   |   +-- FeedbackResponse.cs
|   |   +-- LoginRequest.cs
|   |   +-- PronunciationRequest.cs
|   |   +-- RefreshRequest.cs
|   |   +-- RegisterRequest.cs
|   +-- Exercise.cs
|   +-- Language.cs
|   +-- LanguageConstants.cs
|   +-- Lesson.cs
|   +-- PronunciationAttempt.cs
|   +-- RefreshToken.cs
|   +-- User.cs
|   +-- UserProgress.cs
+-- Repositories/
|   +-- ExerciseRepository.cs
|   +-- IExerciseRepository.cs
|   +-- ILanguageRepository.cs
|   +-- ILessonRepository.cs
|   +-- IProgressRepository.cs
|   +-- IPronunciationRepository.cs
|   +-- IRefreshTokenRepository.cs
|   +-- IUserRepository.cs
|   +-- LanguageRepository.cs
|   +-- LessonRepository.cs
|   +-- ProgressRepository.cs
|   +-- PronunciationRepository.cs
|   +-- RefreshTokenRepository.cs
|   +-- UserRepository.cs
+-- Services/
|   +-- AuthService.cs
|   +-- ExerciseScoringService.cs
|   +-- GroqAiService.cs
|   +-- IAuthService.cs
|   +-- ProgressService.cs
|   +-- PronunciationService.cs
|   +-- TokenService.cs
+-- Settings/
|   +-- GroqSettings.cs
|   +-- JwtSettings.cs
+-- Program.cs
+-- appsettings.json
```

### Frontend (`paolingua-frontend/`)

```
paolingua-frontend/
+-- src/
|   +-- api/
|   |   +-- auth.ts
|   |   +-- client.ts
|   |   +-- exercises.ts
|   |   +-- grammar.ts
|   |   +-- languages.ts
|   |   +-- lessons.ts
|   |   +-- progress.ts
|   |   +-- pronunciation.ts
|   |   +-- user.ts
|   +-- components/
|   |   +-- exercises/
|   |   |   +-- ExerciseRenderer.tsx
|   |   |   +-- GrammarExercise.tsx
|   |   |   +-- PronunciationExercise.tsx
|   |   |   +-- TranslationExercise.tsx
|   |   +-- ui/
|   |   |   +-- Button.tsx
|   |   |   +-- Card.tsx
|   |   |   +-- Input.tsx
|   |   |   +-- Modal.tsx
|   |   +-- AppLayout.tsx
|   |   +-- ProtectedRoute.tsx
|   |   +-- Sidebar.tsx
|   +-- context/
|   |   +-- AuthContext.tsx
|   |   +-- ToastContext.tsx
|   +-- hooks/
|   |   +-- useAudio.ts
|   +-- pages/
|   |   +-- DashboardPage.tsx
|   |   +-- LanguagesPage.tsx
|   |   +-- LeaderboardPage.tsx
|   |   +-- LessonDetailPage.tsx
|   |   +-- LessonsPage.tsx
|   |   +-- LoginPage.tsx
|   |   +-- ProfilePage.tsx
|   |   +-- RegisterPage.tsx
|   +-- types/
|   |   +-- index.ts
|   +-- App.tsx
|   +-- main.tsx
+-- index.html
+-- vite.config.ts
+-- package.json
```

---

## 16. Credenciales de Prueba

- Admin: `admin@tutor.com` / `Admin123`
- Test: `test@test.com` / `123456`
