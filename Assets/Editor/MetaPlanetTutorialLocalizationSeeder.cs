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
        SeedPhrases(Phrases, "Meta tutorial");
    }

    [MenuItem("Tools/Localization/Seed Planet Tutorial")]
    public static void SeedPlanetTutorial()
    {
        SeedPhrases(PlanetPhrases, "Planet tutorial");
    }

    private static void SeedPhrases(PhraseDef[] definitions, string tutorial)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            if (root == null)
            {
                Debug.LogError($"{tutorial} localization seeding failed. Prefab not found: {PrefabPath}");
                return;
            }

            LeanLocalization localization = root.GetComponentInChildren<LeanLocalization>(true);

            if (localization == null)
            {
                Debug.LogError($"{tutorial} localization seeding failed. LeanLocalization component not found in {PrefabPath}");
                return;
            }

            int created = 0;
            int updatedTranslations = 0;
            Dictionary<string, LeanPhrase> phrases = BuildPhraseLookup(root);

            foreach (PhraseDef def in definitions)
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
            Debug.Log($"{tutorial} localization seeded. Created phrases: {created}. Translations written: {updatedTranslations}. Prefab: {PrefabPath}");
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

    private static readonly PhraseDef[] PlanetPhrases =
    {
        new PhraseDef("planet_tutorial_first_visit_dialog", "Вот и наша первая планета. Пока целая!", "Here's our first planet. Still in one piece!", "Ось і наша перша планета. Поки ціла!", "¡Aquí está nuestro primer planeta! ¡De momento sigue intacto!"),
        new PhraseDef("planet_tutorial_open_creation_dialog", "Посмотрим, что здесь можно построить.", "Let's see what we can build here.", "Подивимося, що тут можна побудувати.", "Veamos qué podemos construir aquí."),
        new PhraseDef("planet_tutorial_open_creation_hint", "Нажми сюда.", "Tap here.", "Натисни сюди.", "Pulsa aquí."),
        new PhraseDef("planet_tutorial_earn_coins_hint", "Пора делать деньги! Точнее, тут почему-то шестерёнки. Нажимай на кнопку, чтобы их зарабатывать!", "Time to make money! Except... they're gears for some reason. Tap the button to earn them!", "Час заробляти гроші! Точніше, тут чомусь шестерні. Натискай кнопку, щоб їх заробляти!", "¡Hora de ganar dinero! Aunque aquí, por alguna razón, son engranajes. ¡Pulsa el botón para conseguirlos!"),
        new PhraseDef("planet_tutorial_earned_first_toast", "Первые шестерёнки! Каждое нажатие приносит ещё. Это чудо!", "Our first gears! Every tap earns more. It's a miracle!", "Перші шестерні! Кожне натискання приносить ще. Це диво!", "¡Nuestros primeros engranajes! Cada toque nos da más. ¡Es un milagro!"),
        new PhraseDef("planet_tutorial_buy_small_tree_hint", "Купим первый куст. Теперь каждый клик будет прибыльнее!", "Let's buy our first bush. Now every tap will earn us more!", "Купимо перший кущ. Тепер кожне натискання приноситиме більше!", "¡Compremos nuestro primer arbusto! ¡Ahora ganaremos más con cada toque!"),
        new PhraseDef("planet_tutorial_buy_small_tree_done", "Отлично. Куст уже на планете. Теперь каждое нажатие приносит больше!", "Great! The bush is on the planet. Now every tap earns more!", "Чудово. Кущ уже на планеті. Тепер кожне натискання приносить більше!", "¡Genial! El arbusto ya está en el planeta. ¡Ahora cada toque nos da más!"),
        new PhraseDef("planet_tutorial_earn_more_hint", "Продолжай нажимать. Нам нужно больше шестерёнок! Нам нужны деревья!", "Keep tapping. We need more gears! We need trees!", "Продовжуй натискати. Нам потрібно більше шестерень! Нам потрібні дерева!", "Sigue pulsando. ¡Necesitamos más engranajes! ¡Necesitamos árboles!"),
        new PhraseDef("planet_tutorial_buy_big_tree_hint", "Теперь купим дерево побольше. Ещё больше зелени, ещё больше прибыли!\nНу и планета будет выглядеть гораздо лучше.", "Now let's buy a bigger tree. More greenery, more profit!\nAnd the planet will look much better, too.", "Тепер купімо дерево побільше. Ще більше зелені, ще більше прибутку!\nІ планета виглядатиме набагато краще.", "Ahora compremos un árbol más grande. ¡Más verde, más ganancias!\nY el planeta se verá mucho mejor."),
        new PhraseDef("planet_tutorial_buy_big_tree_done", "Отлично! Наша планета начинает зеленеть!", "Great! Our planet is starting to turn green!", "Чудово! Наша планета починає зеленіти!", "¡Genial! ¡Nuestro planeta empieza a ponerse verde!"),
        new PhraseDef("planet_tutorial_manager_early_open_dialog", "Надоело постоянно нажимать? Есть решение — менеджер!", "Tired of tapping all the time? There's a solution — a manager!", "Набридло постійно натискати? Є рішення — менеджер!", "¿Te has cansado de pulsar todo el tiempo? ¡Hay una solución: un gerente!"),
        new PhraseDef("planet_tutorial_manager_not_enough_money_dialog", "Правда, на его зарплату пока не хватает. Поднажми!", "We can't afford his salary just yet, though. Keep tapping!", "Щоправда, на його зарплату поки не вистачає. Натискай активніше!", "Aunque todavía no nos alcanza para su sueldo. ¡Sigue pulsando!"),
        new PhraseDef("planet_tutorial_buy_manager_hint", "Теперь можем нанять менеджера. Пусть работает!", "Now we can hire a manager. Let him do the work!", "Тепер можемо найняти менеджера. Нехай працює!", "Ahora podemos contratar a un gerente. ¡Que se ponga a trabajar!"),
        new PhraseDef("planet_tutorial_buy_manager_done", "Отлично. Теперь планета будет развиваться гораздо быстрее.", "Great! Now the planet will develop much faster.", "Чудово. Тепер планета розвиватиметься набагато швидше.", "¡Genial! Ahora el planeta se desarrollará mucho más rápido.")
    };

    private static readonly PhraseDef[] Phrases =
    {
        new PhraseDef("meta_tutorial_step0_dialog_1", "Добро пожаловать на мою планету!", "Welcome to my planet!", "Ласкаво просимо на мою планету!", "¡Bienvenido a mi planeta!"),
        new PhraseDef("meta_tutorial_step0_dialog_2", "Купил её под собственную контору. Будем делать тут планеты на заказ.", "I bought it for my own little business. We'll be making planets to order here.", "Купив її під власну контору. Будемо тут створювати планети на замовлення.", "La compré para montar mi propio negocio. Aquí crearemos planetas por encargo."),
        new PhraseDef("meta_tutorial_step0_dialog_3", "Надеюсь, катастрофы начнутся не сразу.", "I just hope the disasters don't start right away.", "Сподіваюся, катастрофи почнуться не одразу.", "Espero que las catástrofes no empiecen de inmediato."),
        new PhraseDef("meta_tutorial_step1_reward_title", "Стартовый капитал", "Startup Capital", "Стартовий капітал", "Capital inicial"),
        new PhraseDef("meta_tutorial_step1_reward_description", "Всё, что удалось наскрести на открытие.", "Everything I managed to scrape together to open up shop.", "Усе, що вдалося нашкребти на відкриття.", "Todo lo que logré juntar para abrir el negocio."),
        new PhraseDef("meta_tutorial_step1_reward_amount", "+{0:0} алмазов", "+{0:0} diamonds", "+{0:0} алмазів", "+{0:0} diamantes"),
        new PhraseDef("meta_tutorial_step1_reward_button", "Получить", "Claim", "Отримати", "Recibir"),
        new PhraseDef("meta_tutorial_step1_dialog_1", "Всё, что осталось после покупки планеты.", "All that's left after buying the planet.", "Усе, що залишилося після купівлі планети.", "Todo lo que queda después de comprar el planeta."),
        new PhraseDef("meta_tutorial_step1_dialog_2", "Последние алмазы. Мой самый нелюбимый вид алмазов!", "My last diamonds. My least favorite kind of diamonds!", "Останні алмази. Мій найнелюбіший вид алмазів!", "Los últimos diamantes. ¡Los diamantes que menos me gustan!"),
        new PhraseDef("meta_tutorial_step1_dialog_3", "Посмотрим, на что их хватит.", "Let's see what we can afford.", "Подивимося, на що їх вистачить.", "Veamos para qué nos alcanza."),
        new PhraseDef("meta_tutorial_step2_dialog_1", "Нам понадобится солидный офис.", "We'll need a proper office.", "Нам знадобиться солідний офіс.", "Necesitaremos una oficina en condiciones."),
        new PhraseDef("meta_tutorial_step2_dialog_2", "Но денег нам хватит только на самый дешевый дом.", "But we can only afford the cheapest house.", "Але грошей нам вистачить лише на найдешевший будинок.", "Pero solo nos alcanza para la casa más barata."),
        new PhraseDef("meta_tutorial_step2_dialog_3", "Жить и работать будем там же. Экономия!", "We'll live and work there too. Talk about saving money!", "Жити й працювати будемо там само. Економія!", "Viviremos y trabajaremos allí mismo. ¡Hay que ahorrar!"),
        new PhraseDef("meta_tutorial_step2_open_variant_hint", "Нажми сюда.", "Tap here.", "Натисни тут.", "Toca aquí."),
        new PhraseDef("meta_tutorial_step3_add_variant_hint", "Закажем первый дом. Начнём с упаковки.", "Let's order our first house. We'll start with the packaging.", "Замовимо перший будинок. Почнемо з упаковки.", "Encarguemos nuestra primera casa. Empecemos por el embalaje."),
        new PhraseDef("meta_tutorial_step3_toast_added", "Заказ доставлен", "Order delivered", "Замовлення доставлено", "Pedido entregado"),
        new PhraseDef("meta_tutorial_step3_done_1", "Отлично.", "Great.", "Чудово.", "¡Genial!"),
        new PhraseDef("meta_tutorial_step3_done_2", "Теперь у нас есть собственная коробка.", "Now we have a box of our very own.", "Тепер у нас є власна коробка.", "Ahora tenemos nuestra propia caja."),
        new PhraseDef("meta_tutorial_step5_box_hint", "Нажми на коробку.", "Tap the box.", "Натисни на коробку.", "Toca la caja."),
        new PhraseDef("meta_tutorial_step5_dialog_1", "Неплохой дом за такие деньги.", "Not a bad house for the price.", "Непоганий будинок за такі гроші.", "No está mal la casa por ese precio."),
        new PhraseDef("meta_tutorial_step5_dialog_2", "Здесь будем жить и принимать заказчиков.", "We'll live here and meet with clients.", "Тут житимемо й прийматимемо замовників.", "Aquí viviremos y recibiremos a los clientes."),
        new PhraseDef("meta_tutorial_step5_dialog_3", "Отдельный офис построим после первых заказов.", "We'll build a separate office after our first orders.", "Окремий офіс збудуємо після перших замовлень.", "Construiremos una oficina aparte después de los primeros encargos."),
        new PhraseDef("meta_tutorial_step5_buy_variant_hint", "Бери этот. На остальные не хватит.", "Go with this one. We can't afford the others.", "Бери цей. На решту не вистачить.", "Elige este. No nos alcanza para los demás."),
        new PhraseDef("meta_tutorial_step5_toast_built", "Дом построен", "House built", "Будинок збудовано", "Casa construida"),
        new PhraseDef("meta_tutorial_step5_done_1", "Отлично! Дом готов.", "Great! The house is ready.", "Чудово! Будинок готовий.", "¡Genial! La casa está lista."),
        new PhraseDef("meta_tutorial_step5_done_2", "Теперь есть где встречать заказчиков.", "Now we have somewhere to meet clients.", "Тепер є де зустрічати замовників.", "Ahora tenemos dónde recibir a los clientes."),
        new PhraseDef("meta_tutorial_step5_done_3", "И уже меньше напоминает кучу коробок.", "And it looks a lot less like a pile of boxes.", "І вже менше нагадує купу коробок.", "Y ya se parece mucho menos a un montón de cajas."),
        new PhraseDef("meta_tutorial_step6_return_dialog_1", "С возвращением! Первая планета ещё цела? Значит, зарабатывать ты уже умеешь.", "Welcome back! Is your first planet still in one piece? Then you already know how to make money.", "З поверненням! Перша планета ще ціла? Отже, заробляти ти вже вмієш.", "¡Qué bueno que hayas vuelto! ¿Tu primer planeta sigue de una pieza? Entonces ya sabes cómo ganar dinero."),
        new PhraseDef("meta_tutorial_step6_return_dialog_2", "Я придумал, как ускорить наш бизнес. И нет, это не уклонение от налогов.", "I've figured out how to speed things up for our business. And no, it's not tax evasion.", "Я придумав, як прискорити наш бізнес. І ні, це не ухилення від сплати податків.", "Se me ocurrió cómo darle un impulso a nuestro negocio. Y no, no es evasión fiscal."),
        new PhraseDef("meta_tutorial_step6_open_shop_hint", "Откроем магазин.", "Let's open the shop.", "Відкриймо магазин.", "Abramos la tienda."),
        new PhraseDef("meta_tutorial_step6_open_upgrade_hint", "Теперь посмотрим улучшения.", "Now let's look at the upgrades.", "Тепер погляньмо на покращення.", "Ahora veamos las mejoras."),
        new PhraseDef("meta_tutorial_step6_dialog_1", "Вот эти здания действительно полезны!", "Now these buildings are actually useful!", "Ось ці будівлі справді корисні!", "¡Estos edificios sí que son útiles!"),
        new PhraseDef("meta_tutorial_step6_dialog_2", "Они усиливают все будущие планеты.", "They give all future planets a boost.", "Вони посилюють усі майбутні планети.", "Potencian todos los planetas futuros."),
        new PhraseDef("meta_tutorial_step6_dialog_3", "Красивый офис такого не умеет.", "A fancy office can't do that.", "Гарний офіс такого не вміє.", "Una oficina bonita no puede hacer eso."),
        new PhraseDef("meta_tutorial_step7_add_upgrade_hint", "Построим кое-что полезное для наших деревьев.", "Let's build something useful for our trees.", "Збудуємо дещо корисне для наших дерев.", "Construyamos algo útil para nuestros árboles."),
        new PhraseDef("meta_tutorial_step7_intro_1", "Эти деревья слишком много отдыхают. Пора увеличить норму выработки кислорода!", "These trees are taking too many breaks. Time to raise their oxygen production quota!", "Ці дерева забагато відпочивають. Час підвищити норму виробництва кисню!", "Estos árboles se toman demasiados descansos. ¡Hora de subirles la cuota de producción de oxígeno!"),
        new PhraseDef("meta_tutorial_step7_intro_2", "Для этого у меня есть специальное здание.", "I've got a special building for that.", "Для цього в мене є спеціальна будівля.", "Tengo un edificio especial para eso."),
        new PhraseDef("meta_tutorial_step7_toast_added", "Исследование добавлено", "Research added", "Дослідження додано", "Investigación añadida"),
        new PhraseDef("meta_tutorial_step7_done_1", "Отлично! Теперь деревья под контролем.", "Great! Now we've got the trees under control.", "Чудово! Тепер дерева під контролем.", "¡Genial! Ya tenemos a los árboles bajo control."),
        new PhraseDef("meta_tutorial_step7_done_2", "И приносят больше валюты.", "And they're bringing in more currency.", "І приносять більше валюти.", "Y generan más moneda."),
        new PhraseDef("meta_tutorial_step8_upgrade_hint", "Но это только первый уровень!\n\nВсего их три. Каждый следующий ещё сильнее увеличит прибыль.", "But that's only level one!\n\nThere are three levels in all. Each new level boosts profits even more.", "Але це лише перший рівень!\n\nУсього їх три. Кожен наступний принесе ще більше прибутку.", "¡Pero este es solo el primer nivel!\n\nHay tres en total. Cada nivel aumenta aún más las ganancias."),
        new PhraseDef("meta_tutorial_step8_toast_upgraded", "Доход планет +10%", "Planet income +10%", "Дохід планет +10%", "Ingresos de planetas +10%"),
        new PhraseDef("meta_tutorial_step8_done_1", "Накопишь денег — приходи. Выжмем из деревьев ещё больше!", "Come back when you've saved up some money. We'll squeeze even more out of those trees!", "Назбираєш грошей — приходь. Вичавимо з дерев іще більше!", "Vuelve cuando hayas ahorrado algo de dinero. ¡Les sacaremos aún más a esos árboles!"),
        new PhraseDef("meta_tutorial_step8_done_2", "Бонус достанется всем будущим планетам. Вот это инвестиция!", "All future planets get the bonus. Now that's an investment!", "Бонус дістанеться всім майбутнім планетам. Оце інвестиція!", "Todos los planetas futuros recibirán la bonificación. ¡Eso sí que es una inversión!"),
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
        new PhraseDef("meta_tutorial_step9_contract_title", "Первый заказ", "First Order", "Перше замовлення", "Primer encargo"),
        new PhraseDef("meta_tutorial_step9_contract_description", "Какой-то робот заказал себе планету. Хочет, чтобы местные производили ему запчасти.", "Some robot ordered a planet. He wants the locals to make spare parts for him.", "Якийсь робот замовив собі планету. Хоче, щоб місцеві виробляли для нього запчастини.", "Un robot ha encargado un planeta. Quiere que sus habitantes fabriquen piezas de repuesto para él."),
        new PhraseDef("meta_tutorial_step9_contract_button", "Принять заказ", "Accept Order", "Прийняти замовлення", "Aceptar encargo"),
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
