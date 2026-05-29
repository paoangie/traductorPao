using System.Text.Json;
using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;

namespace Api_TutorIdiomas.Services
{
    public class TheoryService
    {
        private readonly GroqAiService _groqAiService;
        private readonly IMistakeRepository _mistakeRepo;
        private readonly ILessonRepository _lessonRepo;
        private readonly ILanguageRepository _languageRepo;
        private readonly ILogger<TheoryService> _logger;

        public TheoryService(
            GroqAiService groqAiService,
            IMistakeRepository mistakeRepo,
            ILessonRepository lessonRepo,
            ILanguageRepository languageRepo,
            ILogger<TheoryService> logger)
        {
            _groqAiService = groqAiService;
            _mistakeRepo = mistakeRepo;
            _lessonRepo = lessonRepo;
            _languageRepo = languageRepo;
            _logger = logger;
        }

        public async Task<TheoryContentDto> GetTheoryAsync(int lessonId, Guid userId)
        {
            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            if (!string.IsNullOrEmpty(lesson.TheoryContent) && lesson.TheoryContent != "{}")
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<TheoryContentDto>(lesson.TheoryContent);
                    if (cached != null && cached.Sections.Count > 0)
                    {
                        Console.WriteLine($"[TEORIA] ✅ Lección {lessonId} - Usando caché de BD");
                        _logger.LogInformation("Teoría obtenida desde caché BD para lección {LessonId}", lessonId);
                        return cached;
                    }
                }
                catch (JsonException)
                {
                    Console.WriteLine($"[TEORIA] ⚠️ Lección {lessonId} - Caché corrupto, regenerando...");
                    _logger.LogWarning("Error al deserializar TheoryContent cacheado para lección {LessonId}", lessonId);
                }
            }

            try
            {
                Console.WriteLine($"[TEORIA] 🤖 Lección {lessonId} - Generando con IA (Groq)...");
                var theory = await GenerateTheoryWithAiAsync(lessonId, userId);
                theory = NormalizeTheory(theory, lesson.Title);

                var json = JsonSerializer.Serialize(theory);
                lesson.TheoryContent = json;
                await _lessonRepo.UpdateAsync(lesson);
                await _lessonRepo.SaveChangesAsync();

                Console.WriteLine($"[TEORIA] ✅ Lección {lessonId} - IA generó y guardó en BD");
                return theory;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TEORIA] ❌ Lección {lessonId} - IA falló: {ex.Message}");
                _logger.LogError(ex, "Error al generar teoría con IA para lección {LessonId}", lessonId);
            }

            var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);
            var langName = language?.Name ?? "el idioma";
            Console.WriteLine($"[TEORIA] 📦 Lección {lessonId} - Usando fallback estático para '{langName}'");
            return GetFallbackTheory(lesson.Title, langName, lesson.LanguageId);
        }

        private async Task<TheoryContentDto> GenerateTheoryWithAiAsync(int lessonId, Guid userId)
        {
            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);
            var languageName = language?.Name ?? "el idioma";

            var levelName = lesson.Level switch
            {
                1 => "A1 (Básico)",
                2 => "A2 (Elemental)",
                3 => "B1 (Intermedio)",
                4 => "B2 (Intermedio-Alto)",
                5 => "C1 (Avanzado)",
                6 => "C2 (Maestría)",
                _ => "A1 (Básico)"
            };

            var mistakes = await _mistakeRepo.GetCommonMistakesAsync(userId, lesson.LanguageId);
            var mistakesText = mistakes == null || mistakes.Count == 0
                ? "Ninguno por ahora"
                : string.Join("; ", mistakes.Select(m =>
                    $"\"{m.MistakeText}\" (debería ser \"{m.CorrectText}\", {m.Count} veces)"));

            var prompt = $@"Eres un tutor de idiomas experto. Genera contenido teórico para la lección ""{lesson.Title}"" de {languageName} nivel {levelName}.

Errores frecuentes del estudiante: {mistakesText}

Responde SOLO con JSON válido. Sin markdown, sin texto adicional, sin comillas al inicio/fin.

Estructura exacta requerida:
{{
  ""title"": ""{lesson.Title}"",
  ""introduction"": ""Texto introductorio motivador (2-3 oraciones)"",
  ""sections"": [
    {{
      ""type"": ""vocabulary"",
      ""title"": ""Vocabulario Clave"",
      ""items"": [
        {{""term"": ""palabra en {languageName}"", ""translation"": ""traducción al español"", ""pronunciation"": ""pronunciación IPA"", ""example"": ""ejemplo de uso""}}
      ]
    }},
    {{
      ""type"": ""grammar"",
      ""title"": ""Reglas Gramaticales"",
      ""rules"": [
        {{""explanation"": ""explicación de la regla"", ""examples"": [""ejemplo1"", ""ejemplo2""], ""tip"": ""consejo práctico""}}
      ]
    }},
    {{
      ""type"": ""phrases"",
      ""title"": ""Frases Útiles"",
      ""items"": [
        {{""phrase"": ""frase en {languageName}"", ""translation"": ""traducción al español"", ""context"": ""cuándo usar esta frase""}}
      ]
    }},
    {{
      ""type"": ""cultural_note"",
      ""title"": ""Nota Cultural"",
      ""content"": ""información cultural relevante""
    }}
  ],
  ""summary"": ""Resumen de 2-3 oraciones de los puntos clave""
}}

Reglas importantes:
1. Incluye 4-6 términos de vocabulario relevantes al tema ""{lesson.Title}"".
2. Incluye 1-2 reglas gramaticales con ejemplos claros.
3. Incluye 3-4 frases útiles con contexto de uso.
4. Incluye una nota cultural breve pero interesante.
5. Si el estudiante tiene errores frecuentes, enfatiza esos temas en la teoría.
6. El contenido debe ser apropiado para el nivel {levelName}.
7. Las pronunciaciones en formato IPA estándar.
8. Todo el texto en español, excepto los términos del idioma objetivo que deben estar en {languageName}.";

            var response = await _groqAiService.QueryLlamaAsync(prompt);
            var theory = ParseTheoryResponse(response);

            return theory;
        }

        private TheoryContentDto ParseTheoryResponse(string response)
        {
            var cleaned = response.Trim();
            if (cleaned.StartsWith("```json")) cleaned = cleaned.Substring(7);
            if (cleaned.StartsWith("```")) cleaned = cleaned.Substring(3);
            if (cleaned.EndsWith("```")) cleaned = cleaned.Substring(0, cleaned.Length - 3);
            cleaned = cleaned.Trim();

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var theory = JsonSerializer.Deserialize<TheoryContentDto>(cleaned, options);
                if (theory == null || theory.Sections == null || theory.Sections.Count == 0)
                    throw new JsonException("Teoría vacía o nula");

                return theory;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error al parsear respuesta de teoría de Groq: {Response}", response);
                throw;
            }
        }

        private TheoryContentDto NormalizeTheory(TheoryContentDto theory, string lessonTitle)
        {
            if (string.IsNullOrWhiteSpace(theory.Title))
                theory.Title = lessonTitle;

            theory.Sections ??= new List<TheorySectionDto>();

            foreach (var section in theory.Sections)
            {
                switch (section.Type)
                {
                    case "vocabulary":
                        section.Items ??= new List<VocabularyItemDto>();
                        break;
                    case "grammar":
                        section.Rules ??= new List<GrammarRuleDto>();
                        break;
                    case "phrases":
                        if (section.Phrases == null && section.Items != null)
                        {
                            section.Phrases = section.Items.Select(i => new PhraseItemDto
                            {
                                Phrase = i.Term,
                                Translation = i.Translation,
                                Context = i.Example
                            }).ToList();
                            section.Items = null;
                        }
                        section.Phrases ??= new List<PhraseItemDto>();
                        break;
                }
            }

            return theory;
        }

        private TheoryContentDto GetFallbackTheory(string lessonTitle, string languageName, int languageId)
        {
            var theme = ClassifyTheme(lessonTitle);
            return new TheoryContentDto
            {
                Title = lessonTitle,
                Introduction = $"Bienvenido a la lección sobre \"{lessonTitle}\". A continuación encontrarás el vocabulario, reglas gramaticales y frases útiles en {languageName} que necesitas para dominar este tema.",
                Sections = new List<TheorySectionDto>
                {
                    BuildVocabularySection(languageId, theme),
                    BuildGrammarSection(languageId, theme, languageName),
                    BuildPhrasesSection(languageId, theme, languageName),
                    BuildCulturalSection(languageId, languageName)
                },
                Summary = $"Has aprendido los fundamentos de \"{lessonTitle}\" en {languageName}. Repasa el vocabulario y practica las frases antes de pasar a los ejercicios."
            };
        }

        private static string ClassifyTheme(string title)
        {
            var t = title.ToLower();
            if (t.Contains("saludo")) return "greetings";
            if (t.Contains("número") || t.Contains("contar")) return "numbers";
            if (t.Contains("familia") || t.Contains("amigo")) return "family";
            if (t.Contains("verbo")) return "verbs";
            if (t.Contains("vocabulario") || t.Contains("vocabulaire") || t.Contains("grundwortschatz") || t.Contains("vocabolario")) return "basic_vocab";
            return "greetings";
        }

        private static TheorySectionDto BuildVocabularySection(int langId, string theme)
        {
            var terms = GetVocabularyTerms(langId, theme);
            return new TheorySectionDto
            {
                Type = "vocabulary",
                Title = "Vocabulario Clave",
                Items = terms
            };
        }

        private static List<VocabularyItemDto> GetVocabularyTerms(int langId, string theme)
        {
            return (langId, theme) switch
            {
                // === Inglés (1) ===
                (1, "greetings") => new()
                {
                    new() { Term = "Hello", Translation = "Hola", Pronunciation = "/həˈloʊ/", Example = "Hello, how are you?" },
                    new() { Term = "Good morning", Translation = "Buenos días", Pronunciation = "/ɡʊd ˈmɔːrnɪŋ/", Example = "Good morning, teacher!" },
                    new() { Term = "Goodbye", Translation = "Adiós", Pronunciation = "/ɡʊdˈbaɪ/", Example = "Goodbye, see you later!" },
                    new() { Term = "Nice to meet you", Translation = "Mucho gusto", Pronunciation = "/naɪs tuː miːt juː/", Example = "Nice to meet you, Sarah" },
                    new() { Term = "How are you?", Translation = "¿Cómo estás?", Pronunciation = "/haʊ ɑːr juː/", Example = "Hello! How are you today?" }
                },
                (1, "numbers") => new()
                {
                    new() { Term = "One", Translation = "Uno", Pronunciation = "/wʌn/", Example = "One apple, please" },
                    new() { Term = "Two", Translation = "Dos", Pronunciation = "/tuː/", Example = "I have two brothers" },
                    new() { Term = "Three", Translation = "Tres", Pronunciation = "/θriː/", Example = "There are three cats" },
                    new() { Term = "Ten", Translation = "Diez", Pronunciation = "/tɛn/", Example = "Ten dollars" },
                    new() { Term = "Twenty", Translation = "Veinte", Pronunciation = "/ˈtwɛnti/", Example = "She is twenty years old" },
                    new() { Term = "Count", Translation = "Contar", Pronunciation = "/kaʊnt/", Example = "Let's count from one to ten" }
                },
                (1, "family") => new()
                {
                    new() { Term = "Mother", Translation = "Madre", Pronunciation = "/ˈmʌðər/", Example = "My mother is a teacher" },
                    new() { Term = "Father", Translation = "Padre", Pronunciation = "/ˈfɑːðər/", Example = "His father works in a hospital" },
                    new() { Term = "Brother", Translation = "Hermano", Pronunciation = "/ˈbrʌðər/", Example = "I have two brothers" },
                    new() { Term = "Sister", Translation = "Hermana", Pronunciation = "/ˈsɪstər/", Example = "Her sister is very kind" },
                    new() { Term = "Friend", Translation = "Amigo/a", Pronunciation = "/frɛnd/", Example = "She is my best friend" },
                    new() { Term = "Family", Translation = "Familia", Pronunciation = "/ˈfæməli/", Example = "This is my family" }
                },
                (1, "verbs") => new()
                {
                    new() { Term = "To be", Translation = "Ser/Estar", Pronunciation = "/tuː biː/", Example = "I am happy" },
                    new() { Term = "To have", Translation = "Tener", Pronunciation = "/tuː hæv/", Example = "I have a car" },
                    new() { Term = "To eat", Translation = "Comer", Pronunciation = "/tuː iːt/", Example = "I eat breakfast at 8" },
                    new() { Term = "To drink", Translation = "Beber", Pronunciation = "/tuː drɪŋk/", Example = "I drink water" },
                    new() { Term = "To go", Translation = "Ir", Pronunciation = "/tuː ɡoʊ/", Example = "I go to school" }
                },
                (1, "basic_vocab") => new()
                {
                    new() { Term = "Water", Translation = "Agua", Pronunciation = "/ˈwɔːtər/", Example = "I drink water" },
                    new() { Term = "House", Translation = "Casa", Pronunciation = "/haʊs/", Example = "My house is big" },
                    new() { Term = "Sun", Translation = "Sol", Pronunciation = "/sʌn/", Example = "The sun is bright" },
                    new() { Term = "Book", Translation = "Libro", Pronunciation = "/bʊk/", Example = "I read a book" },
                    new() { Term = "Food", Translation = "Comida", Pronunciation = "/fuːd/", Example = "The food is delicious" },
                    new() { Term = "Dog", Translation = "Perro", Pronunciation = "/dɔːɡ/", Example = "I have a dog" }
                },

                // === Español (2) ===
                (2, "greetings") => new()
                {
                    new() { Term = "Buenos días", Translation = "Good morning", Pronunciation = "/ˈbwenos ˈdias/", Example = "Buenos días, profesor" },
                    new() { Term = "Buenas tardes", Translation = "Good afternoon", Pronunciation = "/ˈbwenas ˈtaɾdes/", Example = "Buenas tardes, señora" },
                    new() { Term = "Buenas noches", Translation = "Good evening/night", Pronunciation = "/ˈbwenas ˈnotʃes/", Example = "Buenas noches, hasta mañana" },
                    new() { Term = "Adiós", Translation = "Goodbye", Pronunciation = "/aˈðjos/", Example = "Adiós, nos vemos" },
                    new() { Term = "Mucho gusto", Translation = "Nice to meet you", Pronunciation = "/ˈmutʃo ˈɡusto/", Example = "Mucho gusto, soy Ana" }
                },
                (2, "basic_vocab") => new()
                {
                    new() { Term = "Agua", Translation = "Water", Pronunciation = "/ˈaɡwa/", Example = "Quiero agua, por favor" },
                    new() { Term = "Casa", Translation = "House", Pronunciation = "/ˈkasa/", Example = "Mi casa es grande" },
                    new() { Term = "Sol", Translation = "Sun", Pronunciation = "/sol/", Example = "El sol brilla mucho" },
                    new() { Term = "Libro", Translation = "Book", Pronunciation = "/ˈlibɾo/", Example = "Leo un libro" },
                    new() { Term = "Comida", Translation = "Food", Pronunciation = "/koˈmiða/", Example = "La comida está rica" }
                },
                (2, "verbs") => new()
                {
                    new() { Term = "Ser", Translation = "To be (permanent)", Pronunciation = "/seɾ/", Example = "Yo soy de Bolivia" },
                    new() { Term = "Estar", Translation = "To be (temporary)", Pronunciation = "/esˈtaɾ/", Example = "Yo estoy en casa" },
                    new() { Term = "Comer", Translation = "To eat", Pronunciation = "/koˈmeɾ/", Example = "Yo como frutas" },
                    new() { Term = "Beber", Translation = "To drink", Pronunciation = "/beˈbeɾ/", Example = "Yo bebo agua" },
                    new() { Term = "Ir", Translation = "To go", Pronunciation = "/iɾ/", Example = "Yo voy a la escuela" }
                },

                // === Francés (3) ===
                (3, "greetings") => new()
                {
                    new() { Term = "Bonjour", Translation = "Buenos días", Pronunciation = "/bɔ̃ʒuʁ/", Example = "Bonjour, comment ça va?" },
                    new() { Term = "Bonsoir", Translation = "Buenas noches", Pronunciation = "/bɔ̃swaʁ/", Example = "Bonsoir, madame" },
                    new() { Term = "Au revoir", Translation = "Adiós", Pronunciation = "/o ʁəvwaʁ/", Example = "Au revoir, à demain!" },
                    new() { Term = "Merci", Translation = "Gracias", Pronunciation = "/mɛʁsi/", Example = "Merci beaucoup!" },
                    new() { Term = "Enchanté", Translation = "Encantado", Pronunciation = "/ɑ̃ʃɑ̃te/", Example = "Enchanté, je suis Pierre" }
                },
                (3, "basic_vocab") => new()
                {
                    new() { Term = "Eau", Translation = "Agua", Pronunciation = "/o/", Example = "Je bois de l'eau" },
                    new() { Term = "Maison", Translation = "Casa", Pronunciation = "/mɛzɔ̃/", Example = "Ma maison est grande" },
                    new() { Term = "Soleil", Translation = "Sol", Pronunciation = "/sɔlɛj/", Example = "Le soleil brille" },
                    new() { Term = "Livre", Translation = "Libro", Pronunciation = "/livʁ/", Example = "Je lis un livre" },
                    new() { Term = "Nourriture", Translation = "Comida", Pronunciation = "/nuʁityʁ/", Example = "La nourriture est bonne" }
                },
                (3, "verbs") => new()
                {
                    new() { Term = "Être", Translation = "Ser/Estar", Pronunciation = "/ɛtʁ/", Example = "Je suis français" },
                    new() { Term = "Avoir", Translation = "Tener", Pronunciation = "/avwaʁ/", Example = "J'ai un livre" },
                    new() { Term = "Manger", Translation = "Comer", Pronunciation = "/mɑ̃ʒe/", Example = "Je mange une pomme" },
                    new() { Term = "Boire", Translation = "Beber", Pronunciation = "/bwaʁ/", Example = "Je bois de l'eau" },
                    new() { Term = "Aller", Translation = "Ir", Pronunciation = "/ale/", Example = "Je vais à l'école" }
                },

                // === Alemán (4) ===
                (4, "greetings") => new()
                {
                    new() { Term = "Hallo", Translation = "Hola", Pronunciation = "/haˈloː/", Example = "Hallo, wie geht's?" },
                    new() { Term = "Guten Morgen", Translation = "Buenos días", Pronunciation = "/ˈɡuːtən ˈmɔʁɡən/", Example = "Guten Morgen, Herr Schmidt!" },
                    new() { Term = "Guten Abend", Translation = "Buenas noches", Pronunciation = "/ˈɡuːtən ˈaːbənt/", Example = "Guten Abend, Frau Müller" },
                    new() { Term = "Auf Wiedersehen", Translation = "Adiós", Pronunciation = "/aʊf ˈviːdɐzeːən/", Example = "Auf Wiedersehen, bis morgen!" },
                    new() { Term = "Danke", Translation = "Gracias", Pronunciation = "/ˈdaŋkə/", Example = "Danke schön!" }
                },
                (4, "basic_vocab") => new()
                {
                    new() { Term = "Wasser", Translation = "Agua", Pronunciation = "/ˈvasɐ/", Example = "Ich trinke Wasser" },
                    new() { Term = "Haus", Translation = "Casa", Pronunciation = "/haʊs/", Example = "Mein Haus ist groß" },
                    new() { Term = "Sonne", Translation = "Sol", Pronunciation = "/ˈzɔnə/", Example = "Die Sonne scheint" },
                    new() { Term = "Buch", Translation = "Libro", Pronunciation = "/buːx/", Example = "Ich lese ein Buch" },
                    new() { Term = "Essen", Translation = "Comida", Pronunciation = "/ˈɛsən/", Example = "Das Essen ist lecker" }
                },
                (4, "verbs") => new()
                {
                    new() { Term = "Sein", Translation = "Ser/Estar", Pronunciation = "/zaɪn/", Example = "Ich bin müde" },
                    new() { Term = "Haben", Translation = "Tener", Pronunciation = "/ˈhaːbən/", Example = "Ich habe ein Buch" },
                    new() { Term = "Essen", Translation = "Comer", Pronunciation = "/ˈɛsən/", Example = "Ich esse einen Apfel" },
                    new() { Term = "Trinken", Translation = "Beber", Pronunciation = "/ˈtʁɪŋkən/", Example = "Ich trinke Wasser" },
                    new() { Term = "Gehen", Translation = "Ir", Pronunciation = "/ˈɡeːən/", Example = "Ich gehe zur Schule" }
                },

                // === Italiano (5) ===
                (5, "greetings") => new()
                {
                    new() { Term = "Ciao", Translation = "Hola/Adiós", Pronunciation = "/ˈtʃaːo/", Example = "Ciao, come stai?" },
                    new() { Term = "Buongiorno", Translation = "Buenos días", Pronunciation = "/bwɔnˈdʒorno/", Example = "Buongiorno, signora!" },
                    new() { Term = "Buonasera", Translation = "Buenas noches", Pronunciation = "/bwɔnaˈseːra/", Example = "Buonasera, signore" },
                    new() { Term = "Arrivederci", Translation = "Adiós (formal)", Pronunciation = "/arriveˈdɛrtʃi/", Example = "Arrivederci, a domani!" },
                    new() { Term = "Grazie", Translation = "Gracias", Pronunciation = "/ˈɡrattsje/", Example = "Grazie mille!" }
                },
                (5, "basic_vocab") => new()
                {
                    new() { Term = "Acqua", Translation = "Agua", Pronunciation = "/ˈakkwa/", Example = "Bevo acqua" },
                    new() { Term = "Casa", Translation = "Casa", Pronunciation = "/ˈkaːsa/", Example = "La mia casa è grande" },
                    new() { Term = "Sole", Translation = "Sol", Pronunciation = "/ˈsoːle/", Example = "Il sole splende" },
                    new() { Term = "Libro", Translation = "Libro", Pronunciation = "/ˈliːbro/", Example = "Leggo un libro" },
                    new() { Term = "Cibo", Translation = "Comida", Pronunciation = "/ˈtʃiːbo/", Example = "Il cibo è buono" }
                },
                (5, "verbs") => new()
                {
                    new() { Term = "Essere", Translation = "Ser/Estar", Pronunciation = "/ˈɛssere/", Example = "Io sono italiano" },
                    new() { Term = "Avere", Translation = "Tener", Pronunciation = "/aˈveːre/", Example = "Io ho un libro" },
                    new() { Term = "Mangiare", Translation = "Comer", Pronunciation = "/manˈdʒaːre/", Example = "Io mangio una mela" },
                    new() { Term = "Bere", Translation = "Beber", Pronunciation = "/ˈbeːre/", Example = "Io bevo acqua" },
                    new() { Term = "Andare", Translation = "Ir", Pronunciation = "/anˈdaːre/", Example = "Io vado a scuola" }
                },

                // === Default / Fallback ===
                _ => new()
                {
                    new() { Term = "Hello", Translation = "Hola", Pronunciation = "/həˈloʊ/", Example = "Hello, how are you?" },
                    new() { Term = "Goodbye", Translation = "Adiós", Pronunciation = "/ɡʊdˈbaɪ/", Example = "Goodbye, see you later!" },
                    new() { Term = "Please", Translation = "Por favor", Pronunciation = "/pliːz/", Example = "Please, help me" },
                    new() { Term = "Thank you", Translation = "Gracias", Pronunciation = "/θæŋk juː/", Example = "Thank you very much" }
                }
            };
        }

        private static TheorySectionDto BuildGrammarSection(int langId, string theme, string languageName)
        {
            var (explanation, examples, tip) = (langId, theme) switch
            {
                (_, "greetings") => (
                    $"En {languageName}, los saludos varían según la hora del día y el nivel de formalidad. Es importante distinguir entre situaciones formales e informales.",
                    new List<string> { "Saludo formal: se usa con desconocidos o personas mayores", "Saludo informal: se usa con amigos y familiares" },
                    "Observa cómo te saludan los demás y adapta tu nivel de formalidad"
                ),
                (_, "numbers") => (
                    $"En {languageName}, los números siguen patrones que, una vez aprendidos, facilitan contar hasta cualquier cifra. Los números del 1 al 10 son la base.",
                    new List<string> { "1-10: los números básicos", "11-20: suelen tener formas especiales" },
                    "Practica contando objetos a tu alrededor para memorizar los números"
                ),
                (_, "family") => (
                    $"En {languageName}, los términos familiares pueden variar según el género y la relación. El posesivo 'my'/'mi'/'mon'/'mein'/'mio' es esencial para describir tu familia.",
                    new List<string> { "My mother → usas 'my' + parentesco", "This is my brother → presentas a un familiar" },
                    "Dibuja tu árbol familiar y etiqueta cada miembro en el idioma que aprendes"
                ),
                (_, "verbs") => (
                    $"En {languageName}, la conjugación verbal es fundamental. Cada persona (yo, tú, él...) tiene una forma diferente del verbo.",
                    new List<string> { "Primera persona (yo): forma única del verbo", "Tercera persona (él/ella): otra forma distinta" },
                    "Aprende primero los verbos más comunes y sus conjugaciones básicas"
                ),
                (_, "basic_vocab") => (
                    $"En {languageName}, el vocabulario básico incluye sustantivos cotidianos como objetos, lugares y elementos de la naturaleza. El artículo (el/la/los/las o sus equivalentes) acompaña al sustantivo.",
                    new List<string> { "Sustantivo + artículo: 'la casa', 'el libro'", "Género: los sustantivos pueden ser masculinos o femeninos" },
                    "Asocia cada palabra nueva con una imagen mental para recordarla mejor"
                ),
                _ => (
                    $"Aprender {languageName} requiere práctica constante y exposición al idioma.",
                    new List<string> { "Practica a diario", "Escucha música y podcast en el idioma" },
                    "La constancia es más importante que la cantidad de tiempo por sesión"
                )
            };

            return new TheorySectionDto
            {
                Type = "grammar",
                Title = "Reglas Gramaticales",
                Rules = new List<GrammarRuleDto>
                {
                    new() { Explanation = explanation, Examples = examples, Tip = tip }
                }
            };
        }

        private static TheorySectionDto BuildPhrasesSection(int langId, string theme, string languageName)
        {
            var phrases = (langId, theme) switch
            {
                (1, "greetings") => new List<PhraseItemDto>
                {
                    new() { Phrase = "How are you?", Translation = "¿Cómo estás?", Context = "Saludo informal después de Hello" },
                    new() { Phrase = "I'm fine, thank you", Translation = "Estoy bien, gracias", Context = "Respuesta típica a 'How are you?'" },
                    new() { Phrase = "What's your name?", Translation = "¿Cómo te llamas?", Context = "Para preguntar el nombre" },
                    new() { Phrase = "My name is...", Translation = "Me llamo...", Context = "Para presentarte" }
                },
                (1, "numbers") => new List<PhraseItemDto>
                {
                    new() { Phrase = "How much is it?", Translation = "¿Cuánto cuesta?", Context = "Para preguntar precio" },
                    new() { Phrase = "I have two brothers", Translation = "Tengo dos hermanos", Context = "Para hablar de tu familia" },
                    new() { Phrase = "What's your phone number?", Translation = "¿Cuál es tu número de teléfono?", Context = "Para pedir el número de contacto" }
                },
                (1, "family") => new List<PhraseItemDto>
                {
                    new() { Phrase = "This is my family", Translation = "Esta es mi familia", Context = "Al presentar a tu familia" },
                    new() { Phrase = "I have two brothers", Translation = "Tengo dos hermanos", Context = "Hablando de hermanos" },
                    new() { Phrase = "My mother is a teacher", Translation = "Mi madre es profesora", Context = "Describiendo profesiones de la familia" }
                },
                (1, "verbs") => new List<PhraseItemDto>
                {
                    new() { Phrase = "I am happy", Translation = "Estoy feliz", Context = "Expresar emociones con 'to be'" },
                    new() { Phrase = "I have a book", Translation = "Tengo un libro", Context = "Posesión con 'to have'" },
                    new() { Phrase = "I like to read", Translation = "Me gusta leer", Context = "Expresar gustos" }
                },
                (1, "basic_vocab") => new List<PhraseItemDto>
                {
                    new() { Phrase = "The house is big", Translation = "La casa es grande", Context = "Describir objetos" },
                    new() { Phrase = "I drink water", Translation = "Bebo agua", Context = "Acciones cotidianas" },
                    new() { Phrase = "Where is the book?", Translation = "¿Dónde está el libro?", Context = "Preguntar ubicación" }
                },

                // === Español (2) ===
                (2, "greetings") => new List<PhraseItemDto>
                {
                    new() { Phrase = "¿Cómo estás?", Translation = "How are you?", Context = "Saludo informal" },
                    new() { Phrase = "Mucho gusto", Translation = "Nice to meet you", Context = "Al conocer a alguien" },
                    new() { Phrase = "¿Cómo te llamas?", Translation = "What's your name?", Context = "Para preguntar el nombre" },
                    new() { Phrase = "Me llamo...", Translation = "My name is...", Context = "Para presentarte" }
                },
                (2, "basic_vocab") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Quiero agua", Translation = "I want water", Context = "Pedir agua" },
                    new() { Phrase = "La casa es grande", Translation = "The house is big", Context = "Describir una casa" },
                    new() { Phrase = "¿Dónde está el libro?", Translation = "Where is the book?", Context = "Preguntar ubicación" }
                },
                (2, "verbs") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Yo soy de Bolivia", Translation = "I am from Bolivia", Context = "Decir de dónde eres" },
                    new() { Phrase = "Yo como frutas", Translation = "I eat fruits", Context = "Hablar de hábitos" },
                    new() { Phrase = "Yo bebo agua", Translation = "I drink water", Context = "Acción cotidiana" }
                },

                // === Francés (3) ===
                (3, "greetings") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Comment allez-vous?", Translation = "¿Cómo está? (formal)", Context = "Saludo formal" },
                    new() { Phrase = "Je m'appelle...", Translation = "Me llamo...", Context = "Para presentarte" },
                    new() { Phrase = "Enchanté", Translation = "Encantado/a", Context = "Al conocer a alguien" },
                    new() { Phrase = "Comment ça va?", Translation = "¿Cómo va? (informal)", Context = "Saludo entre amigos" }
                },
                (3, "basic_vocab") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Je bois de l'eau", Translation = "Bebo agua", Context = "Pedir agua" },
                    new() { Phrase = "La maison est grande", Translation = "La casa es grande", Context = "Describir una casa" },
                    new() { Phrase = "Où est le livre?", Translation = "¿Dónde está el libro?", Context = "Preguntar ubicación" }
                },
                (3, "verbs") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Je suis français", Translation = "Soy francés", Context = "Decir tu nacionalidad" },
                    new() { Phrase = "J'ai un livre", Translation = "Tengo un libro", Context = "Posesión" },
                    new() { Phrase = "Je mange une pomme", Translation = "Como una manzana", Context = "Hablar de comida" }
                },

                // === Alemán (4) ===
                (4, "greetings") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Wie geht es Ihnen?", Translation = "¿Cómo está? (formal)", Context = "Saludo formal" },
                    new() { Phrase = "Ich heiße...", Translation = "Me llamo...", Context = "Para presentarte" },
                    new() { Phrase = "Freut mich", Translation = "Mucho gusto", Context = "Al conocer a alguien" },
                    new() { Phrase = "Wie geht's?", Translation = "¿Cómo va? (informal)", Context = "Saludo entre amigos" }
                },
                (4, "basic_vocab") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Ich trinke Wasser", Translation = "Bebo agua", Context = "Pedir agua" },
                    new() { Phrase = "Das Haus ist groß", Translation = "La casa es grande", Context = "Describir una casa" },
                    new() { Phrase = "Wo ist das Buch?", Translation = "¿Dónde está el libro?", Context = "Preguntar ubicación" }
                },
                (4, "verbs") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Ich bin müde", Translation = "Estoy cansado", Context = "Expresar estado" },
                    new() { Phrase = "Ich habe ein Buch", Translation = "Tengo un libro", Context = "Posesión" },
                    new() { Phrase = "Ich esse einen Apfel", Translation = "Como una manzana", Context = "Hablar de comida" }
                },

                // === Italiano (5) ===
                (5, "greetings") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Come stai?", Translation = "¿Cómo estás?", Context = "Saludo informal" },
                    new() { Phrase = "Mi chiamo...", Translation = "Me llamo...", Context = "Para presentarte" },
                    new() { Phrase = "Piacere", Translation = "Mucho gusto", Context = "Al conocer a alguien" },
                    new() { Phrase = "Come va?", Translation = "¿Cómo va?", Context = "Saludo casual" }
                },
                (5, "basic_vocab") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Bevo acqua", Translation = "Bebo agua", Context = "Pedir agua" },
                    new() { Phrase = "La casa è grande", Translation = "La casa es grande", Context = "Describir una casa" },
                    new() { Phrase = "Dov'è il libro?", Translation = "¿Dónde está el libro?", Context = "Preguntar ubicación" }
                },
                (5, "verbs") => new List<PhraseItemDto>
                {
                    new() { Phrase = "Io sono italiano", Translation = "Soy italiano", Context = "Decir tu nacionalidad" },
                    new() { Phrase = "Io ho un libro", Translation = "Tengo un libro", Context = "Posesión" },
                    new() { Phrase = "Io mangio una mela", Translation = "Como una manzana", Context = "Hablar de comida" }
                },

                _ => new List<PhraseItemDto>
                {
                    new() { Phrase = "Hello!", Translation = "¡Hola!", Context = "Saludo universal" },
                    new() { Phrase = "Thank you", Translation = "Gracias", Context = "Agradecimiento" },
                    new() { Phrase = "How are you?", Translation = "¿Cómo estás?", Context = "Conversación casual" }
                }
            };

            return new TheorySectionDto
            {
                Type = "phrases",
                Title = "Frases Útiles",
                Phrases = phrases
            };
        }

        private static TheorySectionDto BuildCulturalSection(int langId, string languageName)
        {
            var content = langId switch
            {
                1 => $"En países de habla inglesa (Reino Unido, EE. UU., Australia, etc.), es común sonreír al saludar y mantener contacto visual. El espacio personal suele ser mayor que en culturas latinas, y 'How are you?' es más un saludo que una pregunta real. Responder 'I'm fine, thanks' es aceptable incluso si no estás bien.",
                2 => $"En el mundo hispanohablante, los saludos suelen ser más cálidos y cercanos. Es común dar un beso en la mejilla (dependiendo del país) o un abrazo entre amigos. 'Buenos días' se usa hasta el mediodía, 'Buenas tardes' hasta el anochecer, y 'Buenas noches' al despedirse o llegar de noche.",
                3 => $"En Francia, la cortesía es muy importante. Siempre saluda con 'Bonjour' al entrar a una tienda o restaurante. El beso en la mejilla ('la bise') varía según la región: puede ser de 2 a 4 besos. Usar 'vous' (usted) con desconocidos muestra respeto.",
                4 => $"En Alemania, la puntualidad es fundamental. Los saludos suelen ser más formales que en otras culturas, especialmente en entornos laborales. Un apretón de manos firme es el saludo estándar en contextos profesionales. 'Guten Tag' se usa durante el día.",
                5 => $"En Italia, la comunicación es expresiva y apasionada. Los italianos suelen saludar con dos besos en las mejillas, incluso entre hombres. La gestualización con las manos acompaña frecuentemente la conversación. 'Ciao' es versátil: sirve tanto para hola como para adiós en contextos informales.",
                _ => $"Cada cultura tiene sus propias normas de cortesía y comunicación. Es importante observar y adaptarse al contexto cultural para comunicarse efectivamente."
            };

            return new TheorySectionDto
            {
                Type = "cultural_note",
                Title = "Nota Cultural",
                Content = content
            };
        }
    }
}
