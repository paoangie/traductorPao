# Documentación del Backend - Tutor de Idiomas API

Stack: .NET 8 + PostgreSQL + Entity Framework Core + Groq AI API

Puerto: `http://localhost:14567`
BD: PostgreSQL puerto 5433, Base de datos: `TutorIdiomasDB`

---

## Modelos (7 entidades)

| Modelo | Descripción |
|--------|-------------|
| `User` | Usuario con Id (Guid), Email, PasswordHash, Role, CreatedAt |
| `RefreshToken` | Tokens de refresco JWT asociados a User |
| `Language` | Idiomas: Name, Code (en/es/fr/de/it), FlagIcon |
| `Lesson` | Lecciones: LanguageId, Title, Level, XpReward |
| `Exercise` | Ejercicios: LessonId, Type (pronunciation/translation/grammar), Content (JSONB) |
| `UserProgress` | Progreso: UserId, LanguageId, LessonId, Score, Completed, CompletedAt |
| `PronunciationAttempt` | Intentos de pronunciación: UserId, ExerciseId, AudioUrl, RecognizedText, ExpectedText, Score |

---

## Autenticación (JWT)

**Endpoints:**
- `POST /api/auth/register` — Registro con email/password
- `POST /api/auth/login` — Login, devuelve AccessToken + RefreshToken
- `POST /api/auth/refresh` — Renovar tokens con RefreshToken
- `POST /api/auth/revoke` — Revocar RefreshToken
- `GET /api/auth/me` — Obtener usuario actual

**Tokens:** AccessToken (60 min) + RefreshToken (7 días, almacenado en BD)

---

## Setup / Inicialización

**Endpoints (sin autenticación):**

1. `POST /api/setup/inicializar-sistema`
   Crea 5 idiomas (Inglés, Español, Francés, Alemán, Italiano), 7 lecciones, 29 ejercicios, usuario admin (`admin@tutor.com` / `Admin123`) y test (`test@test.com` / `123456`).

2. `POST /api/setup/llenar-ejercicios`
   Endpoint adicional por si faltan lecciones 6-7 (Alemán/Italiano) y sus ejercicios.

3. `GET /api/setup/verificar-estado`
   Ver cuántos registros hay en cada tabla.

---

## Flujo de Pronunciación (Audio)

```
Frontend (JS)                           Backend (C#)
     |                                      |
     |  1. Graba audio (MediaRecorder)       |
     |  2. Convierte a Base64                |
     |  POST /api/pronunciation/evaluate     |
     |  { audioBase64, expectedPhrase,       |
     |    exerciseId }                       |
     |─────────────────────────────────────>|
     |                                      |
     |                    3. Decodifica Base64 -> byte[]
     |                    4. Envía a Groq Whisper API
     |                       (audio/webm, whisper-large-v3)
     |                    5. Whisper devuelve texto reconocido
     |                    6. Envía a Groq Llama para feedback
     |                    7. Calcula score con Levenshtein
     |                    8. Guarda PronunciationAttempt en BD
     |                                      |
     |  <───────────────────────────────────|
     |  { recognizedText, score,             |
     |    grammarFeedback, suggestions }      |
```

**Detalles técnicos del audio:**
- Llega como **Base64** desde el frontend
- Se convierte a `byte[]` y se envía a Groq como **audio/webm**
- **NO se guarda el archivo de audio en disco ni en BD**, solo el texto reconocido (`AudioUrl = "inline"`)
- El frontend debería usar `MediaRecorder` con formato `audio/webm`

**Puntuación:** Algoritmo **Levenshtein** (distancia de edición) entre texto reconocido y esperado.

**Otros endpoints de pronunciación:**
- `POST /api/pronunciation/practice-word` — Practicar palabra específica con Groq
- `GET /api/pronunciation/history` — Historial de intentos del usuario
- `GET /api/pronunciation/history/{exerciseId}` — Intentos por ejercicio
- `GET /api/pronunciation/stats` — Estadísticas globales
- `GET /api/pronunciation/difficult-words` — Palabras con bajo score

---

## Ejercicios (envío de respuestas)

`POST /api/exercises/{id}/submit`
```json
{ "userAnswer": "...", "expectedPhrase": "...", "timeSpentSeconds": 0 }
```

Según el tipo:
- **translation** — Compara palabras clave, score parcial
- **grammar** — Coincidencia exacta (0 o 100)
- **pronunciation** — Similaridad caracter por caracter

Luego guarda en `UserProgress` y devuelve `{ score, feedback, correct }`.

---

## Progreso y Gamificación

- `GET /api/progress/me` — Nivel, XP, racha, % completado
- `GET /api/progress/me/language/{id}` — Progreso por idioma
- `POST /api/progress/lesson/{id}/complete` — Completar lección, suma XP
- `GET /api/progress/streak` — Racha de días consecutivos
- `GET /api/progress/leaderboard` — Ranking de usuarios

**Sistema de niveles:** Nivel = (XP total / 500) + 1

---

## Groq AI (Servicio externo)

API Key configurada en `appsettings.Development.json` (no incluido en el repositorio):
```json
{
  "ApiKey": "tu-api-key-aqui",
  "WhisperModel": "whisper-large-v3",
  "LlamaModel": "llama-3.3-70b-versatile",
  "ApiUrl": "https://api.groq.com/openai/v1"
}
```

**Usos de Groq:**
1. **Whisper** (`POST /audio/transcriptions`) — Transcribe audio a texto
2. **Llama** (`POST /chat/completions`) — Feedback de gramática, corrección, sugerencias

---

## Otros Endpoints

| Método | Ruta | Auth |
|--------|------|------|
| GET | `/api/languages` | ✅ |
| GET | `/api/languages/{id}/lessons` | ✅ |
| GET | `/api/lessons/language/{id}` | ✅ (incluye progreso) |
| GET | `/api/exercises/lesson/{id}` | ✅ |
| POST | `/api/grammar/correct` | ✅ |

---

## Estructura del Proyecto

```
Api_TutorIdiomas/
├── Controllers/
│   ├── AuthController.cs        # Login, register, refresh, revoke
│   ├── ExercisesController.cs   # Ejercicios por lección, submit respuesta
│   ├── GrammarController.cs     # Corrección gramatical vía Groq
│   ├── LanguagesController.cs   # CRUD de idiomas
│   ├── LessonsController.cs     # Lecciones por idioma
│   ├── ProgressController.cs    # Progreso, XP, racha, leaderboard
│   ├── PronunciationController.cs # Evaluación de pronunciación
│   ├── SetupController.cs       # Inicialización del sistema
│   └── UserController.cs        # Perfil y cambio de contraseña
├── Data/
│   └── BdContext.cs             # DbContext de EF Core
├── Migrations/
│   ├── 20260519112930_p1.cs
│   ├── 20260519112930_p1.Designer.cs
│   └── BdContextModelSnapshot.cs
├── Models/
│   ├── Exercise.cs
│   ├── Language.cs
│   ├── Lesson.cs
│   ├── PronunciationAttempt.cs
│   ├── RefreshToken.cs
│   ├── User.cs
│   ├── UserProgress.cs
│   └── DTOs/
│       ├── AuthResponse.cs
│       ├── ExerciseSubmitDto.cs
│       ├── FeedbackResponse.cs
│       ├── LoginRequest.cs
│       ├── PronunciationRequest.cs
│       ├── RefreshRequest.cs
│       └── RegisterRequest.cs
├── Repositories/
│   ├── ExerciseRepository.cs
│   ├── LanguageRepository.cs
│   ├── LessonRepository.cs
│   ├── ProgressRepository.cs
│   ├── PronunciationRepository.cs
│   ├── RefreshTokenRepository.cs
│   └── UserRepository.cs
├── Services/
│   ├── AuthService.cs           # Lógica de autenticación
│   ├── GroqAiService.cs         # Integración con Groq API
│   ├── ProgressService.cs       # Lógica de progreso
│   ├── PronunciationService.cs  # Lógica de evaluación de pronunciación
│   └── TokenService.cs          # Generación de JWT
├── Settings/
│   ├── GroqSettings.cs
│   └── JwtSettings.cs
├── Program.cs                   # Configuración general, DI, CORS, Swagger
└── appsettings.json             # Conexión BD, JWT, Groq
```

---

## Configuración Destacada

- `Program.cs`: Migraciones automáticas al iniciar, CORS multi-origen, JsonOptions con `IgnoreCycles`
- Las migraciones se aplican solas al iniciar si hay pendientes
- BD: PostgreSQL puerto 5433, JWT configurado con clave simétrica

---

## Credenciales de Prueba

- Admin: `admin@tutor.com` / `Admin123`
- Test: `test@test.com` / `123456`
