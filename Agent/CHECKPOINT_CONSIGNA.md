# Checkpoint: Implementación contra la Consigna

## 1. Stack Tecnológico

| Tecnología | Uso | Estado |
|------------|-----|--------|
| React (Web) | Frontend — Componentes reutilizables | Completado |
| ASP.NET Core (Web API) | Backend — Servicios y controladores |Completado  |
| PostgreSQL | Base de datos — Usuarios, progreso, ejercicios | Completado |
| Groq API (Whisper-large-v3) | Voz a texto para pronunciación | Completado |
| Groq API (Llama 3) | Corrección gramatical y feedback adaptativo | Completado |

---

## 2. Capas de Arquitectura (15 Pts)

| Capa | Ubicación | Descripción |
|------|-----------|-------------|
| **Presentación** | `Controllers/` | 9 controladores con endpoints REST |
| **Servicios** | `Services/` | Lógica de negocio y comunicación con IA |
| **Datos** | `Repositories/` + `Data/BdContext.cs` | Acceso a PostgreSQL vía EF Core |

### Diagrama de Arquitectura

```
React App (Frontend)
      │
      │ HTTP (JWT Auth)
      ▼
ASP.NET Core Web API ─────────────────────┐
  │                                        │
  ├─ Controllers/  (Routing + Auth)        │
  ├─ Services/     (Lógica de IA) ─────► Groq API
  ├─ Repositories/ (Acceso a datos)        │  ├─ Whisper (audio→texto)
  └─ Data/BdContext (EF Core)              │  └─ Llama  (feedback)
         │                                  │
         ▼                                  │
    PostgreSQL                              │
  (Users, Languages, Lessons,               │
   Exercises, Progress,                     │
   PronunciationAttempts)                   │
                                            │
         ◄──────────────────────────────────┘
```

---

## 3. Componentes Reutilizables (30 Pts)

### Componente 1: ChatbotConnectorService (Backend)

**Archivo:** `Services/GroqAiService.cs`

**Propósito:** Clase genérica que recibe un Prompt y un Modelo y devuelve la respuesta de la IA.

```csharp
// Método genérico — acepta cualquier prompt y cualquier modelo
public async Task<string> QueryAiAsync(string prompt, string model)
{
    var requestBody = new
    {
        model,  // ← Parámetro: "llama-3.3-70b-versatile", "mixtral-8x7b", etc.
        messages = new[] { new { role = "user", content = prompt } },
        temperature = 0.7,
        max_tokens = 500
    };
    // ... llamada HTTP a Groq API
}

// Método de conveniencia para Llama (usa el modelo por defecto del config)
public async Task<string> QueryLlamaAsync(string prompt)
    => await QueryAiAsync(prompt, _groqSettings.LlamaModel);
```

**Reutilización:** El mismo método sirve para corregir gramática, traducir, generar ejercicios nuevos, o incluso para un futuro "Tutor de Matemáticas" — solo cambia el prompt y el modelo.

---

### Componente 2: ExerciseRenderer (Frontend — Pendiente)

Componente de React que recibe un JSON con una pregunta (completar, traducir o hablar) y lo renderiza en pantalla.

---

### Componente 3: AudioProcessor (Frontend — Pendiente)

Hook/Utilidad de React para capturar audio del micrófono con métodos `StartRecording()` y `GetAudioBlob()`.

---

## 4. Técnicas de Refactorización (25 Pts)

### Técnica 1: Extract Method

**Problema:** El controlador `ExercisesController.SubmitAnswer()` tenía la validación, la lógica de scoring y el guardado en BD todo junto en un solo método (~80 líneas).

**Antes (código desordenado):**
```csharp
// ExercisesController.cs — ANTES
[HttpPost("{id}/submit")]
public async Task<IActionResult> SubmitAnswer(int id, [FromBody] ExerciseSubmitDto request)
{
    // ... validación ...
    
    int score;
    string feedback;
    
    if (exercise.Type == "translation")
    {
        var content = JsonSerializer.Deserialize<...>(exercise.Content);
        var expectedAnswer = content?["answer"] ?? "";
        // lógica de scoring inline
        score = CalculateTranslationScore(request.UserAnswer, expectedAnswer);
        feedback = score >= 70 ? "¡Correcto! ✅" : "Respuesta incorrecta 📚";
    }
    else if (exercise.Type == "grammar")
    {
        // otra lógica inline...
    }
    else if (exercise.Type == "pronunciation")
    {
        // otra lógica inline...
    }
    
    await _progressRepo.UpdateExerciseScoreAsync(userId, id, score);
    // ...
}
```

**Después (código limpio — método extraído a servicio propio):**
```csharp
// ExerciseScoringService.cs — NUEVO SERVICIO
public class ExerciseScoringService
{
    public ExerciseScoreResult EvaluateTranslation(string userAnswer, string exerciseContent) { ... }
    public ExerciseScoreResult EvaluateGrammar(string userAnswer, string exerciseContent) { ... }
    public ExerciseScoreResult EvaluatePronunciation(string userAnswer, string? expectedPhrase) { ... }
}

// ExercisesController.cs — DESPUÉS
[HttpPost("{id}/submit")]
public async Task<IActionResult> SubmitAnswer(int id, [FromBody] ExerciseSubmitDto request)
{
    var result = exercise.Type switch
    {
        "translation"   => _scoringService.EvaluateTranslation(request.UserAnswer, exercise.Content),
        "grammar"       => _scoringService.EvaluateGrammar(request.UserAnswer, exercise.Content),
        "pronunciation" => _scoringService.EvaluatePronunciation(request.UserAnswer, request.ExpectedPhrase),
        _               => throw new ArgumentException($"Tipo desconocido: {exercise.Type}")
    };
    
    await _progressRepo.UpdateExerciseScoreAsync(userId, id, result.Score);
    return Ok(new { result.Score, result.Feedback, correct = result.Score >= 70 });
}
```

**Beneficio:** El controller queda limpio (solo orquestación), la lógica de scoring es testeable de forma independiente y reutilizable desde cualquier otro controller.

---

### Técnica 2: Replace Magic Number with Symbolic Constant

**Problema:** Se usaban números literales `1`, `2`, `3`, `4`, `5` para identificar idiomas en `SetupController.cs`.

**Antes (código con números mágicos):**
```csharp
var lessons = new[]
{
    new Lesson { Id = 1, LanguageId = 1, Title = "Saludos y Presentaciones", ... },
    new Lesson { Id = 2, LanguageId = 1, Title = "Números y Contar", ... },
    new Lesson { Id = 4, LanguageId = 2, Title = "Saludos en Español", ... },
    new Lesson { Id = 5, LanguageId = 3, Title = "Bonjour! Saludos", ... },
};
```

**Después (constantes con nombre significativo):**
```csharp
// Models/LanguageConstants.cs — NUEVO
public static class LanguageConstants
{
    public const int Ingles   = 1;
    public const int Espanol  = 2;
    public const int Frances  = 3;
    public const int Aleman   = 4;
    public const int Italiano = 5;
}

// SetupController.cs — DESPUÉS
var lessons = new[]
{
    new Lesson { Id = 1, LanguageId = LanguageConstants.Ingles,   Title = "Saludos y Presentaciones", ... },
    new Lesson { Id = 2, LanguageId = LanguageConstants.Ingles,   Title = "Números y Contar", ... },
    new Lesson { Id = 4, LanguageId = LanguageConstants.Espanol,  Title = "Saludos en Español", ... },
    new Lesson { Id = 5, LanguageId = LanguageConstants.Frances,  Title = "Bonjour! Saludos", ... },
};
```

**Beneficio:** El código se autodocumenta. Si en el futuro se cambia el ID de un idioma, solo se modifica en un lugar.

---

## 5. Base de Datos

| Tabla | Columnas | Estado |
|-------|----------|--------|
| **Users** | Id (Guid), **Name** (varchar 100), Email (varchar 200), PasswordHash, Role, CreatedAt | ✅ |
| **Languages** | Id, Name, Code (ISO), FlagIcon | ✅ |
| **Lessons** | Id, LanguageId, Title, Level, XpReward | ✅ |
| **Exercises** | Id, LessonId, Type, Content (JSONB) | ✅ |
| **UserProgress** | Id, UserId, LanguageId, LessonId, Score, Completed, CompletedAt | ✅ |
| **PronunciationAttempts** | Id, UserId, ExerciseId, AudioUrl, RecognizedText, ExpectedText, Score, CreatedAt | ✅ |
| **RefreshTokens** | Id, Token, Expires, IsRevoked, UserId | ✅ |

---

## 6. Manejo de Errores

| Mecanismo | Archivo | Cobertura |
|-----------|---------|-----------|
| Middleware global | `Middleware/ExceptionMiddleware.cs` | Captura y formatea **toda** excepción no manejada (401, 404, 409, 500, 502, 504) |
| Filtro de validación | `Filters/ValidationFilter.cs` | Valida automáticamente DataAnnotations en todos los DTOs |
| try-catch específicos | `Controllers/*.cs` | Cada endpoint maneja FormatException (token inválido), HttpRequestException (502), UnauthorizedAccess, ArgumentException, etc. |
| Timeouts IA | `Services/GroqAiService.cs` | Timeout de 30s para Llama, 60s para Whisper |

---

## 7. Endpoints de la API

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/api/auth/register` | ❌ | Registro de usuario |
| POST | `/api/auth/login` | ❌ | Login (devuelve JWT) |
| POST | `/api/auth/refresh` | ❌ | Renovar tokens |
| POST | `/api/auth/revoke` | ✅ | Revocar token |
| GET | `/api/auth/me` | ✅ | Usuario actual |
| GET | `/api/languages` | ✅ | Lista de idiomas |
| GET | `/api/languages/{id}/lessons` | ✅ | Lecciones por idioma |
| GET | `/api/lessons/language/{id}` | ✅ | Lecciones con progreso |
| GET | `/api/exercises/lesson/{id}` | ✅ | Ejercicios por lección |
| POST | `/api/exercises/{id}/submit` | ✅ | Enviar respuesta |
| POST | `/api/pronunciation/evaluate` | ✅ | Evaluar pronunciación (audio Base64 → Whisper → feedback) |
| POST | `/api/pronunciation/practice-word` | ✅ | Practicar palabra |
| GET | `/api/pronunciation/history` | ✅ | Historial de intentos |
| GET | `/api/pronunciation/stats` | ✅ | Estadísticas |
| POST | `/api/grammar/correct` | ✅ | Corrección gramatical |
| GET | `/api/progress/me` | ✅ | Progreso del usuario |
| POST | `/api/progress/lesson/{id}/complete` | ✅ | Completar lección |
| GET | `/api/progress/leaderboard` | ✅ | Ranking de usuarios |
| POST | `/api/setup/inicializar-sistema` | ❌ | Inicializar BD con datos de prueba |

---

## 8. Estructura Final del Proyecto

```
Api_TutorIdiomas/
├── Controllers/
│   ├── AuthController.cs
│   ├── ExercisesController.cs
│   ├── GrammarController.cs
│   ├── LanguagesController.cs
│   ├── LessonsController.cs
│   ├── ProgressController.cs
│   ├── PronunciationController.cs
│   ├── SetupController.cs
│   └── UserController.cs
├── Data/
│   └── BdContext.cs
├── Filters/
│   └── ValidationFilter.cs              ← NUEVO
├── Middleware/
│   └── ExceptionMiddleware.cs           ← NUEVO
├── Migrations/
│   ├── 20260519112930_p1.cs
│   ├── 20260519221653_AddUserNombre.cs  ← NUEVA
│   └── BdContextModelSnapshot.cs
├── Models/
│   ├── DTOs/ (7 DTOs con validaciones)
│   ├── Exercise.cs
│   ├── Language.cs
│   ├── LanguageConstants.cs             ← NUEVO
│   ├── Lesson.cs
│   ├── PronunciationAttempt.cs
│   ├── RefreshToken.cs
│   ├── User.cs                          ← MODIFICADO (+ Name)
│   └── UserProgress.cs
├── Repositories/ (7 repositorios con interfaces)
├── Services/
│   ├── AuthService.cs
│   ├── ExerciseScoringService.cs        ← NUEVO (Extract Method)
│   ├── GroqAiService.cs                ← MODIFICADO (+ QueryAiAsync genérico)
│   ├── ProgressService.cs
│   ├── PronunciationService.cs
│   └── TokenService.cs
├── Settings/
│   ├── GroqSettings.cs
│   └── JwtSettings.cs
├── Program.cs                          ← MODIFICADO (+ middleware, filter, servicios)
├── appsettings.json
├── DOCUMENTACION_BACKEND.md
└── CHECKPOINT_CONSIGNA.md
```
