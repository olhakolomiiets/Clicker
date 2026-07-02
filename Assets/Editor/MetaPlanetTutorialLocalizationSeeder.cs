using System.Collections.Generic;
using Lean.Localization;
using UnityEditor;
using UnityEngine;

public static class MetaPlanetTutorialLocalizationSeeder
{
    private const string PrefabPath = "Assets/_Prefabs/UI/LeanLocalization.prefab";

    private sealed class PhraseDef
    {
        public readonly string Key;
        public readonly string Russian;
        public readonly string English;
        public readonly string Ukrainian;
        public readonly string Spanish;

        public PhraseDef(string key, string russian, string english, string ukrainian, string spanish)
        {
            Key = key;
            Russian = russian;
            English = english;
            Ukrainian = ukrainian;
            Spanish = spanish;
        }
    }

    [MenuItem("Tools/Localization/Seed Meta Planet Tutorial")]
    public static void Seed()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            if (root == null)
            {
                Debug.LogError($"Meta tutorial localization seeding failed. Prefab not found: {PrefabPath}");
                return;
            }

            LeanLocalization localization = root.GetComponentInChildren<LeanLocalization>(true);

            if (localization == null)
            {
                Debug.LogError($"Meta tutorial localization seeding failed. LeanLocalization component not found in {PrefabPath}");
                return;
            }

            int created = 0;
            int updatedTranslations = 0;
            Dictionary<string, LeanPhrase> phrases = BuildPhraseLookup(root);

            foreach (PhraseDef def in Phrases)
            {
                if (!phrases.TryGetValue(def.Key, out LeanPhrase phrase) || phrase == null)
                {
                    GameObject phraseObject = new GameObject(def.Key);
                    phraseObject.transform.SetParent(localization.transform, false);
                    phrase = phraseObject.AddComponent<LeanPhrase>();
                    phrase.Data = LeanPhrase.DataType.Text;
                    phrases[def.Key] = phrase;
                    created++;
                }

                phrase.name = def.Key;
                phrase.Data = LeanPhrase.DataType.Text;
                AddOrUpdate(phrase, "Russian", def.Russian);
                AddOrUpdate(phrase, "English", def.English);
                AddOrUpdate(phrase, "Ukrainian", def.Ukrainian);
                AddOrUpdate(phrase, "Spanish", def.Spanish);
                updatedTranslations += 4;
                EditorUtility.SetDirty(phrase);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"Meta tutorial localization seeded. Created phrases: {created}. Translations written: {updatedTranslations}. Prefab: {PrefabPath}");
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Dictionary<string, LeanPhrase> BuildPhraseLookup(GameObject root)
    {
        Dictionary<string, LeanPhrase> result = new Dictionary<string, LeanPhrase>();
        LeanPhrase[] existingPhrases = root.GetComponentsInChildren<LeanPhrase>(true);

        foreach (LeanPhrase phrase in existingPhrases)
        {
            if (phrase == null || string.IsNullOrEmpty(phrase.name))
                continue;

            if (!result.ContainsKey(phrase.name))
                result.Add(phrase.name, phrase);
        }

        return result;
    }

    private static void AddOrUpdate(LeanPhrase phrase, string language, string text)
    {
        phrase.AddEntry(language, text);
    }

    private static readonly PhraseDef[] Phrases =
    {
        new PhraseDef("meta_tutorial_step0_dialog_1", "Добро пожаловать в мой исследовательский центр!", "Welcome to my research center!", "Ласкаво просимо до мого дослідницького центру!", "Bienvenido a mi centro de investigación."),
        new PhraseDef("meta_tutorial_step0_dialog_2", "Здесь создаются технологии для будущих планет.", "This is where we build technology for future planets.", "Тут створюються технології для майбутніх планет.", "Aquí creamos tecnología para los planetas del futuro."),
        new PhraseDef("meta_tutorial_step0_dialog_3", "И иногда происходят научные происшествия.", "And where scientific incidents occasionally happen.", "Іноді тут трапляються наукові пригоди.", "Y a veces ocurren incidentes científicos."),
        new PhraseDef("meta_tutorial_step0_dialog_4", "Но сегодня мы просто познакомимся с базой.", "But today, we are just getting familiar with the base.", "Але сьогодні ми просто познайомимося з базою.", "Pero hoy solo vamos a conocer la base."),
        new PhraseDef("meta_tutorial_step1_reward_title", "Стартовый бюджет", "Starting Budget", "Стартовий бюджет", "Presupuesto inicial"),
        new PhraseDef("meta_tutorial_step1_reward_description", "Стартовый резерв станции открыт для развития.", "The station's starter reserve is ready for development.", "Стартовий резерв станції відкрито для розвитку.", "La reserva inicial de la estación ya está lista para el desarrollo."),
        new PhraseDef("meta_tutorial_step1_reward_amount", "+{0:0} алмазов", "+{0:0} diamonds", "+{0:0} алмазів", "+{0:0} diamantes"),
        new PhraseDef("meta_tutorial_step1_reward_button", "Получить", "Claim", "Отримати", "Recibir"),
        new PhraseDef("meta_tutorial_step1_dialog_1", "Бесплатные алмазы.", "Free diamonds.", "Безкоштовні алмази.", "Diamantes gratis."),
        new PhraseDef("meta_tutorial_step1_dialog_2", "Мой любимый вид алмазов.", "My favorite kind of diamonds.", "Мій улюблений вид алмазів.", "Mi tipo favorito de diamantes."),
        new PhraseDef("meta_tutorial_step1_dialog_3", "Покажу куда их можно потратить.", "I will show you where we can spend them.", "Покажу, куди їх можна витратити.", "Te mostraré dónde podemos gastarlos."),
        new PhraseDef("meta_tutorial_step2_dialog_1", "Сначала посмотрим на варианты объектов.", "First, let's look at object variants.", "Спершу подивимося на варіанти об'єктів.", "Primero veamos las variantes de objetos."),
        new PhraseDef("meta_tutorial_step2_dialog_2", "Они позволяют менять внешний вид станции.", "They let us change the station's appearance.", "Вони дозволяють змінювати вигляд станції.", "Nos permiten cambiar el aspecto de la estación."),
        new PhraseDef("meta_tutorial_step2_dialog_3", "Практической пользы немного.", "They are not especially practical.", "Практичної користі небагато.", "No tienen demasiada utilidad práctica."),
        new PhraseDef("meta_tutorial_step2_dialog_4", "Но выглядят красиво.", "But they do look good.", "Зате виглядають красиво.", "Pero se ven muy bien."),
        new PhraseDef("meta_tutorial_step2_open_variant_hint", "Нажми сюда.", "Tap here.", "Натисни тут.", "Toca aquí."),
        new PhraseDef("meta_tutorial_step3_add_variant_hint", "Для начала разместим новый объект.\n\nПока это просто коробка с доставкой. Содержимое узнаем после распаковки.", "First, let's place a new object.\n\nFor now it is just a delivery box. We will see what is inside after unpacking.", "Для початку розмістимо новий об'єкт.\n\nПоки що це просто коробка з доставкою. Що всередині, дізнаємося після розпакування.", "Para empezar, coloquemos un objeto nuevo.\n\nPor ahora es solo una caja de entrega. Veremos qué hay dentro al abrirla."),
        new PhraseDef("meta_tutorial_step3_toast_added", "Объект доставлен", "Object delivered", "Об'єкт доставлено", "Objeto entregado"),
        new PhraseDef("meta_tutorial_step3_done_1", "Отлично.", "Great.", "Чудово.", "Muy bien."),
        new PhraseDef("meta_tutorial_step3_done_2", "Теперь объект появился на станции.", "Now the object is on the station.", "Тепер об'єкт з'явився на станції.", "Ahora el objeto está en la estación."),
        new PhraseDef("meta_tutorial_step5_box_hint", "Нажми на коробку.", "Tap the box.", "Натисни на коробку.", "Toca la caja."),
        new PhraseDef("meta_tutorial_step5_dialog_1", "Теперь выберем внешний вид.", "Now let's choose its appearance.", "Тепер виберемо зовнішній вигляд.", "Ahora elegiremos su aspecto."),
        new PhraseDef("meta_tutorial_step5_dialog_2", "Это не влияет на бонусы.", "This does not affect bonuses.", "Це не впливає на бонуси.", "Esto no afecta a las bonificaciones."),
        new PhraseDef("meta_tutorial_step5_dialog_3", "Но станция будет выглядеть гораздо лучше.", "But the station will look much better.", "Але станція виглядатиме значно краще.", "Pero la estación quedará mucho mejor."),
        new PhraseDef("meta_tutorial_step5_buy_variant_hint", "Выбери этот вариант.", "Choose this variant.", "Вибери цей варіант.", "Elige esta variante."),
        new PhraseDef("meta_tutorial_step5_toast_built", "Дом построен", "House built", "Будинок побудовано", "Casa construida"),
        new PhraseDef("meta_tutorial_step5_done_1", "Отлично.", "Great.", "Чудово.", "Muy bien."),
        new PhraseDef("meta_tutorial_step5_done_2", "Теперь это выглядит как настоящая база.", "Now it looks like a real base.", "Тепер це схоже на справжню базу.", "Ahora parece una base de verdad."),
        new PhraseDef("meta_tutorial_step5_done_3", "И значительно меньше напоминает коробку.", "And much less like a box.", "І значно менше схоже на коробку.", "Y mucho menos como una caja."),
        new PhraseDef("meta_tutorial_step6_open_shop_hint", "Откроем улучшения.", "Open upgrades.", "Відкриймо покращення.", "Abre las mejoras."),
        new PhraseDef("meta_tutorial_step6_open_upgrade_hint", "Откроем улучшения.", "Open upgrades.", "Відкриймо покращення.", "Abre las mejoras."),
        new PhraseDef("meta_tutorial_step6_dialog_1", "А вот это уже действительно полезно.", "Now this is actually useful.", "А ось це вже справді корисно.", "Esto sí que es realmente útil."),
        new PhraseDef("meta_tutorial_step6_dialog_2", "Улучшения влияют на все будущие планеты.", "Upgrades affect all future planets.", "Покращення впливають на всі майбутні планети.", "Las mejoras afectan a todos los planetas futuros."),
        new PhraseDef("meta_tutorial_step6_dialog_3", "Именно здесь начинается настоящий прогресс.", "This is where real progress begins.", "Саме тут починається справжній прогрес.", "Aquí empieza el progreso de verdad."),
        new PhraseDef("meta_tutorial_step7_add_upgrade_hint", "Добавим новое исследование.", "Add a new research item.", "Додамо нове дослідження.", "Añade una nueva investigación."),
        new PhraseDef("meta_tutorial_step7_intro_1", "Каждое улучшение представляет отдельное направление исследований.", "Each upgrade represents a separate field of research.", "Кожне покращення представляє окремий напрям досліджень.", "Cada mejora representa una línea de investigación distinta."),
        new PhraseDef("meta_tutorial_step7_intro_2", "Сначала разместим его на станции.", "First, let's place it on the station.", "Спершу розмістимо його на станції.", "Primero coloquémosla en la estación."),
        new PhraseDef("meta_tutorial_step7_toast_added", "Исследование добавлено", "Research added", "Дослідження додано", "Investigación añadida"),
        new PhraseDef("meta_tutorial_step7_done_1", "Отлично.", "Great.", "Чудово.", "Muy bien."),
        new PhraseDef("meta_tutorial_step7_done_2", "Теперь улучшение доступно для развития.", "Now the upgrade can be developed.", "Тепер покращення доступне для розвитку.", "Ahora la mejora se puede desarrollar."),
        new PhraseDef("meta_tutorial_step8_upgrade_hint", "Каждое улучшение можно прокачать до трёх уровней.\n\nКаждый уровень усиливает бонус.", "Each upgrade can be raised to three levels.\n\nEach level strengthens the bonus.", "Кожне покращення можна прокачати до трьох рівнів.\n\nКожен рівень посилює бонус.", "Cada mejora puede subirse hasta el nivel tres.\n\nCada nivel refuerza la bonificación."),
        new PhraseDef("meta_tutorial_step8_toast_upgraded", "Доход планет +10%", "Planet income +10%", "Дохід планет +10%", "Ingresos de planetas +10%"),
        new PhraseDef("meta_tutorial_step8_done_1", "Видишь?", "See?", "Бачиш?", "¿Ves?"),
        new PhraseDef("meta_tutorial_step8_done_2", "Этот бонус будет работать на всех будущих планетах.", "This bonus will apply to all future planets.", "Цей бонус працюватиме на всіх майбутніх планетах.", "Esta bonificación funcionará en todos los planetas futuros."),
        new PhraseDef("meta_tutorial_step8_done_3", "Поэтому улучшения важнее красивых домов.", "That is why upgrades matter more than pretty houses.", "Тому покращення важливіші за красиві будинки.", "Por eso las mejoras importan más que las casas bonitas."),
        new PhraseDef("meta_tutorial_step8_done_4", "Хотя красивые дома тоже хороши.", "Although pretty houses are good too.", "Хоча красиві будинки теж хороші.", "Aunque las casas bonitas también están bien."),
        new PhraseDef("meta_tutorial_step9_dialog_1", "Вы принимаете заказы на создание планет?", "Do you accept orders to create planets?", "Ви приймаєте замовлення на створення планет?", "¿Aceptan encargos para crear planetas?"),
        new PhraseDef("meta_tutorial_step9_dialog_2", "Конечно принимаем.", "Of course we do.", "Звісно, приймаємо.", "Claro que sí."),
        new PhraseDef("meta_tutorial_step9_dialog_3", "Это буквально единственная причина существования этой станции.", "It is literally the only reason this station exists.", "Це буквально єдина причина існування цієї станції.", "Es literalmente la única razón por la que existe esta estación."),
        new PhraseDef("meta_tutorial_step9_dialog_4", "Мне нужна планета с разумными существами.", "I need a planet with intelligent beings.", "Мені потрібна планета з розумними істотами.", "Necesito un planeta con seres inteligentes."),
        new PhraseDef("meta_tutorial_step9_dialog_5", "Они должны производить детали для ремонта моего корпуса.", "They must produce parts to repair my chassis.", "Вони мають виробляти деталі для ремонту мого корпусу.", "Deben producir piezas para reparar mi chasis."),
        new PhraseDef("meta_tutorial_step9_dialog_6", "Прекрасный заказ.", "Excellent order.", "Прекрасне замовлення.", "Un encargo excelente."),
        new PhraseDef("meta_tutorial_step9_dialog_7", "Что может пойти не так?", "What could possibly go wrong?", "Що може піти не так?", "¿Qué podría salir mal?"),
        new PhraseDef("meta_tutorial_step9_dialog_8", "Меня немного пугает ваш энтузиазм.", "Your enthusiasm is making me somewhat uneasy.", "Мене трохи лякає ваш ентузіазм.", "Su entusiasmo me inquieta un poco."),
        new PhraseDef("meta_tutorial_step9_dialog_9", "Меня тоже.", "Me too.", "Мене теж.", "A mí también."),
        new PhraseDef("meta_tutorial_step9_dialog_10", "Но обычно уже слишком поздно что-либо менять.", "But usually it is already too late to change anything.", "Але зазвичай вже запізно щось змінювати.", "Pero normalmente ya es tarde para cambiar nada."),
        new PhraseDef("meta_tutorial_step9_contract_title", "Первый заказ", "First Order", "Перше замовлення", "Primer pedido"),
        new PhraseDef("meta_tutorial_step9_contract_description", "Создать планету с разумными существами для производства деталей.", "Create a planet with intelligent beings to produce parts.", "Створити планету з розумними істотами для виробництва деталей.", "Crear un planeta con seres inteligentes para fabricar piezas."),
        new PhraseDef("meta_tutorial_step9_contract_button", "Принять заказ", "Accept Order", "Прийняти замовлення", "Aceptar pedido"),
        new PhraseDef("meta_tutorial_step10_dialog_1", "Вот примерно этим мы и занимаемся.", "That is basically what we do here.", "Приблизно цим ми тут і займаємося.", "Eso es básicamente lo que hacemos aquí."),
        new PhraseDef("meta_tutorial_step10_dialog_2", "Создаём планеты.", "We create planets.", "Створюємо планети.", "Creamos planetas."),
        new PhraseDef("meta_tutorial_step10_dialog_3", "Развиваем цивилизации.", "We grow civilizations.", "Розвиваємо цивілізації.", "Desarrollamos civilizaciones."),
        new PhraseDef("meta_tutorial_step10_dialog_4", "Выполняем заказы клиентов.", "We complete client orders.", "Виконуємо замовлення клієнтів.", "Cumplimos pedidos de clientes."),
        new PhraseDef("meta_tutorial_step10_dialog_5", "Каждая новая планета отличается.", "Every new planet is different.", "Кожна нова планета відрізняється.", "Cada planeta nuevo es distinto."),
        new PhraseDef("meta_tutorial_step10_dialog_6", "У каждой свои ресурсы.", "Each one has its own resources.", "У кожної свої ресурси.", "Cada uno tiene sus propios recursos."),
        new PhraseDef("meta_tutorial_step10_dialog_7", "Свои жители.", "Its own people.", "Своїх жителів.", "Sus propios habitantes."),
        new PhraseDef("meta_tutorial_step10_dialog_8", "Свои проблемы.", "Its own problems.", "Свої проблеми.", "Sus propios problemas."),
        new PhraseDef("meta_tutorial_step10_dialog_9", "Иногда очень странные.", "Sometimes very strange ones.", "Іноді дуже дивні.", "A veces, muy raros."),
        new PhraseDef("meta_tutorial_step10_dialog_10", "А улучшения на Meta Planet помогают быстрее развивать все будущие миры.", "And Meta Planet upgrades help every future world grow faster.", "А покращення на Meta Planet допомагають швидше розвивати всі майбутні світи.", "Y las mejoras de Meta Planet ayudan a todos los mundos futuros a crecer más rápido."),
        new PhraseDef("meta_tutorial_step10_dialog_11", "Поэтому не забывай возвращаться сюда.", "So do not forget to come back here.", "Тож не забувай повертатися сюди.", "Así que no olvides volver aquí."),
        new PhraseDef("meta_tutorial_step11_dialog_1", "Ну что.", "All right.", "Ну що ж.", "Muy bien."),
        new PhraseDef("meta_tutorial_step11_dialog_2", "Теория закончилась.", "Theory is over.", "Теорія закінчилася.", "Se acabó la teoría."),
        new PhraseDef("meta_tutorial_step11_dialog_3", "Пора посмотреть, что у тебя получится на практике.", "Time to see what you can do in practice.", "Час побачити, що в тебе вийде на практиці.", "Hora de ver qué puedes hacer en la práctica."),
        new PhraseDef("meta_tutorial_step11_dialog_4", "Твой первый заказ уже ждёт.", "Your first order is waiting.", "Твоє перше замовлення вже чекає.", "Tu primer pedido te espera."),
        new PhraseDef("meta_tutorial_step11_dialog_5", "Постарайся не устроить зомби-апокалипсис раньше третьей попытки.", "Try not to start a zombie apocalypse before the third attempt.", "Постарайся не влаштувати зомбі-апокаліпсис раніше третьої спроби.", "Intenta no causar un apocalipsis zombi antes del tercer intento."),
        new PhraseDef("meta_tutorial_step11_contract_title", "Первый контракт", "First Contract", "Перший контракт", "Primer contrato"),
        new PhraseDef("meta_tutorial_step11_contract_description", "Создать цивилизацию для производства деталей.", "Create a civilization to produce parts.", "Створити цивілізацію для виробництва деталей.", "Crear una civilización para fabricar piezas."),
        new PhraseDef("meta_tutorial_step11_contract_button", "Начать работу", "Start Work", "Почати роботу", "Empezar"),
        new PhraseDef("meta_tutorial_video_continue", "Продолжить", "Continue", "Продовжити", "Continuar"),
        new PhraseDef("meta_tutorial_video_skip", "Пропустить", "Skip", "Пропустити", "Omitir")
    };
}
