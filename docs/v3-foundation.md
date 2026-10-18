# TheAppManager v3 : document de fondation

> Ce document est basé sur l'analyse de la v2.2.0 (branche `dev`, commit `99fcb50`), réalisée sous six angles : API, cycle de vie, tests/DX, packaging/AOT, écosystème, docs/maintenance. Une revue contradictoire a vérifié chaque angle, puis un critique de complétude a cherché ce qui manquait. Les sévérités indiquées sont celles qui restent **après vérification**. Les affirmations réfutées sont listées en annexe. Sauf mention contraire, les chemins sont relatifs à `src/TheAppManager/`.

---

## 1. L'essence

TheAppManager découpe `Program.cs` en modules ordonnés pour ASP.NET Core minimal hosting.

- **Un seul concept.** L'interface `IAppModule` a trois hooks optionnels, implémentés par défaut comme no-op (`Modules/IAppModule.cs:12-37`) :
  - `ConfigureServices(WebApplicationBuilder)`
  - `ConfigureMiddleware(WebApplication)`
  - `ConfigureEndpoints(IEndpointRouteBuilder)`
- **Une seule boucle.** `ModulePipeline.Build` (`Modules/ModulePipeline.cs:13-35`) exécute les phases en largeur et dans l'ordre d'enregistrement : tous les `ConfigureServices`, puis `builder.Build()`, puis tous les middlewares, puis tous les endpoints.
- **Tout le reste est de l'emballage** : `AppManager`, `AppManagerBuilder`, `ModuleDiscovery` et `AppModuleTestHost`. Le tout fait environ 580 lignes sur 7 fichiers et produit une DLL de 16 Ko.

**Ce que la bibliothèque promet :** une classe porte les trois points de contact d'une feature au démarrage. Une liste ordonnée et visible les compose. On peut tester n'importe quel sous-ensemble de cette liste.

**Ce qui la différencie vraiment :**
- **Face à de simples méthodes d'extension** (convention Aspire `builder.AddX()` / `app.MapX()`) :
  - un seul type relie services, middleware et endpoints d'une feature ;
  - l'ordre d'exécution est explicite ;
  - on peut faire des **slice tests** : démarrer quelques modules sur un `TestServer` et en remplacer un (`Replace<TOld,TNew>`), ce que `WebApplicationFactory` ne sait pas faire.
- **Face à la concurrence :**
  - plus complet que Carter, qui ne gère que les routes ;
  - beaucoup plus léger qu'ABP ou OrchardCore, qui imposent un graphe de modules, un `DependsOn`, de l'ordre numérique ou du multi-tenant.

**Ce qui ne la différencie pas :** `Start`/`Run`/`ConfigureBuilder` ne font que ce qu'ASP.NET offre déjà (`Startup/AppManager.cs:26-101`).

**Contexte décisif.** L'adoption externe est pratiquement nulle :
- 1 975 téléchargements NuGet, tous sur 2.0.0 ; 2.1.0 et 2.2.0 en ont 0 ;
- aucun `.csproj` externe ne référence le paquet ;
- 2 étoiles sur GitHub.

Casser la compatibilité ne coûte donc presque rien.

---

## 2. Points forts (à préserver en v3)

| # | Point fort | Preuve | Pourquoi le garder |
|---|---|---|---|
| F1 | **Un seul concept, des phases optionnelles** | `IAppModule.cs:18-36`. Les modules du sample implémentent 2 phases sur 3. `ConfigureServices` reçoit le builder complet (Configuration, Environment). | C'est le cœur de l'idée. Toute la courbe d'apprentissage tient en trois méthodes. |
| F2 | **Un pipeline unique, partagé entre prod et tests** | `ModulePipeline.cs:13-35`, appelé par `Startup/AppManagerBuilder.cs:56` et `Testing/AppModuleTestHost.cs:93`. Unifié en #59/#64. | Il n'y a qu'une seule source de vérité sur l'ordre des phases. En v3, il faut le rendre public au lieu de le dupliquer. Réserve : la création du builder diffère encore entre prod et tests (voir W4). |
| F3 | **L'ordre d'exécution est l'ordre d'enregistrement, sans nombres de priorité** | `Modules/AppModuleCollection.cs:12` (une `List`), `Replace` qui garde l'index et le prédicat (`:115-119`), README:78. | C'est l'opposé d'OrchardCore (`Order`) et d'ABP (tri implicite). Ce contrat n'est vrai que pour l'enregistrement explicite : la découverte trie par ordre alphabétique (`ModuleDiscovery.cs:23`). |
| F4 | **`AddIf` avec prédicat évalué sur le builder** | `AppModuleCollection.cs:62-67`, évalué une seule fois dans `GetModules(builder)` (`:134-137`, appelé à `ModulePipeline.cs:15`) après les callbacks `ConfigureBuilder`. La liste filtrée est réutilisée pour les trois phases. | Les conditions sur l'environnement ou la configuration fonctionnent, et un module ne peut pas être actif dans une phase et absent d'une autre. |
| F5 | **Slice tests rapides au niveau du module** | `new AppModuleTestHost().Add<X>().StartAsync()` (`AppModuleTestHostTests.cs:13-24`) prend 5 à 50 ms par test. `Replace` garde la position et la condition (`ReplaceModuleTests.cs:16-32, 141-152`). | C'est la seule chose que `WebApplicationFactory` (WAF) ne fait pas. Formulation honnête : « utilisable, avec des lacunes claires » (voir W3, W4, W5). |
| F6 | **L'enregistrement explicite est compatible trimming et AOT par construction** | Contrainte `new()` + `new TModule()` (`AppModuleCollection.cs:19-23, 62-67`). Avec `IsAotCompatible=true`, les seules alertes sont dans `ModuleDiscovery.cs` (IL2067/IL2026/IL2070). | Le chemin principal de la v3 est déjà sain : seule la découverte pose problème. |
| F7 | **Empreinte presque nulle** | DLL de 16 384 octets. Dépendance au shared framework, plus TestHost (voir W9). Rien n'est ajouté par requête. | Pas de wrapper de conteneur DI ni de service runtime. Cette légèreté est l'argument face à ABP, à protéger. |
| F8 | **L'écosystème ASP.NET reste accessible depuis un module** (relevé par le critique) | Expériences : `MapGroup("/billing")` dans `ConfigureEndpoints` donne `/billing/x` 200 et `/x` 404. `AddOptions<T>().BindConfiguration().ValidateOnStart()` fait échouer `StartAsync` avec `OptionsValidationException`. Une extension `AddBilling(this AppModuleCollection)` sert de *bundle* (2 modules). | Il ne faut **pas** ajouter de hook options/validation, de concept « module de modules » ou de groupes propriétaires : le framework les fournit déjà. Ces patterns sont à documenter. |
| F9 | **WAF fonctionne déjà avec l'enregistrement explicite** | Expérience avec le sample non modifié : `WebApplicationFactory<Program>` donne 200 sur `/weatherforecast`, et `ConfigureTestServices` est bien pris en compte. | C'est un contrat à garantir par un test dans le dépôt. `Mvc.Testing` est référencé (`tests/...csproj:9`) mais aucun test ne l'utilise. |
| F10 | **Pipeline de release moderne** | release-please, plus publication depuis le tag avec NuGet Trusted Publishing OIDC (`.github/workflows/release-please.yml:42-84`). Tests rejoués avant le pack. Build Release avec 0 warning, nullable activé, XML doc complète. | À garder. Réserve : seul `NuGet/login` est épinglé par SHA. |

---

## 3. Points faibles (dédupliqués, classés du plus grave au moins grave)

`[breaking]` signale une direction qui casse l'API v2.

### Sévérité haute

**W1. La couche Startup cache `WebApplication` et on ne peut pas appliquer de modules à un builder qu'on a créé soi-même.**
- **Preuve :**
  - `ModulePipeline` est `internal` (`ModulePipeline.cs:8`).
  - `AppManagerBuilder` force `WebApplication.CreateBuilder(args)` (`AppManagerBuilder.cs:22`) : pas de `WebApplicationOptions`, pas de Slim ni d'Empty builder.
  - L'instance `AppManager` n'expose que `Run`/`RunAsync` sur un `_app` privé (`AppManager.cs:15-48`). Elle n'implémente pas `IAsyncDisposable`, donc un `Build()` qui n'est jamais suivi de `Run()` laisse fuiter l'hôte.
  - `Start` et `StartAsync` dupliquent le même corps (`:62-101`).
  - Il n'existe aucun hook après le build.
  - Un AOT `CreateSlimBuilder` pèse 9,68 Mo contre 16,07 Mo pour `CreateBuilder`, et le premier est inatteignable.
- **Impact :** l'adoption est en tout ou rien. Ça ne marche pas avec Aspire, avec un `Program.cs` existant, avec les builders Slim/Empty ou avec `ApplicationName`/`ContentRoot`. Pour faire `app.Services` ou une migration avant `Run`, il faut écrire un module. La bibliothèque concurrence le `Program.cs` idiomatique au lieu de s'y intégrer.
- **Direction v3 [breaking] :**
  - la primitive devient des extensions : `builder.AddModules(m => …)` puis `app.UseModules()`, ou `builder.BuildWithModules(modules)` qui retourne la vraie `WebApplication` ;
  - on supprime `AppManagerBuilder` et l'instance `AppManager` ;
  - on garde au plus un sucre `AppManager.RunAsync(args, m => …)` construit sur ces extensions.

**W2. La découverte implicite via `Assembly.GetEntryAssembly()` produit une application vide, en silence.**
- **Preuve :**
  - `AppManager.cs:113-120` : si `configure == null`, la bibliothèque scanne l'assembly d'entrée ; si cet assembly est null ou que le scan ne trouve rien, elle démarre sans rien journaliser.
  - Reproduit trois fois : `AppManager.Start(args)` plus `PingModule`, sous `WebApplicationFactory<Program>`, renvoie `GET /ping` 404 avec `entry=testhost`. Le même code lancé avec `dotnet run` renvoie 200.
  - Piège annexe : un delegate `configure` vide désactive aussi la découverte, alors que la XML doc `:52` dit le contraire.
- **Impact :** le chemin « zéro config » mis en avant par le README (README:135-139) est justement celui qu'on ne peut pas tester avec l'outil standard. Le symptôme ressemble à un bug de routage. Aucun test ne couvre la branche `configure == null`.
- **Direction v3 [breaking] :**
  - supprimer la découverte implicite et ancrer explicitement l'assembly (`AddFromAssemblyOf<Program>()` ou un générateur) ;
  - lever une exception si une découverte demandée ne trouve aucun module.

**W3. La liste de modules de production n'est pas réutilisable dans les tests.**
- **Preuve :**
  - La composition vit dans une lambda inline passée à `Start` (sample `Program.cs:5-11`).
  - `AppModuleTestHost` a une collection privée (`AppModuleTestHost.cs:16`) et n'accepte ni `AppModuleCollection` ni `AddFromAssembly`.
  - Sous WAF, `Replace` est inatteignable, car la collection est créée à l'intérieur de `AppManager.cs:105`.
  - Le contournement qui consiste à énumérer la collection perd les prédicats `AddIf` (`AppModuleCollection.cs:140`).
- **Impact :** les deux outils couvrent chacun une moitié du besoin. Le test host sait remplacer un module mais ne fait pas tourner la vraie application ; WAF fait tourner la vraie application mais ne sait pas remplacer de module. Les listes recopiées à la main divergent de la production. **C'est le point central de conception de la v3 côté tests.**
- **Direction v3 [breaking] :**
  - la composition devient une valeur nommée et réutilisable, consommée à la fois par l'hôte de prod et par le test host ;
  - un helper `TheAppManager.Testing` dérivé de WAF expose `ReplaceModule<TOld,TNew>()`.

**W4. L'environnement du test host n'a rien à voir avec la production.**
- **Preuve :**
  - `WebApplication.CreateBuilder()` sans arguments ni options (`AppModuleTestHost.cs:85`).
  - On obtient env=`Production`, `ApplicationName=testhost` et un content root dans le bin du test.
  - `b.WebHost.UseEnvironment(...)` lève `NotSupportedException`. Changer `EnvironmentName` après coup ne charge pas `appsettings.Development.json` et n'active pas `ValidateScopes`.
  - Résultat : un service *scoped* se résout depuis la racine dans le test host, alors que WAF lève une exception. Le README apprend justement à faire ça (README ~285-291, `WeatherForecastService` est enregistré en `AddScoped`).
- **Impact :** le code qui dépend de l'environnement (`SwaggerModule.cs:18`) et les fichiers de configuration ne sont pas testés fidèlement. Les bugs de durée de vie (captive dependency) passent inaperçus.
- **Direction v3 [breaking] :**
  - le test host accepte `WebApplicationOptions` (environnement, content root, args) avant de créer le builder ;
  - il utilise Development par défaut et active `ValidateScopes`/`ValidateOnBuild` ;
  - il expose `Services` et `CreateScope()` au lieu de `GetRequiredService`.

### Sévérité moyenne

**W5. Les overrides passent avant les modules, et il n'existe pas de hook après les modules.**
- **Preuve :** les callbacks `ConfigureBuilder` s'exécutent à `AppManagerBuilder.cs:51-54` et `AppModuleTestHost.cs:88-91`, avant `ModulePipeline.cs:17-20`. `ConfigureBuilder(b => b.Services.AddSingleton<IClock, FakeClock>())` suivi de `Add<ClockModule>()` donne `RealClock`.
- **Nuance vérifiée :** ajouter un module d'override en dernier fonctionne (`.Add(new Fake())` donne `FakeClock`). C'est donc un manque d'ergonomie, pas une impossibilité.
- **Impact :** on doit écrire une classe par override. L'exemple README `FakeAuthModule` perd silencieusement `UseAuthentication()` (README ~297-311).
- **Direction v3 :** ajouter un hook `ConfigureTestServices`/`PostConfigureServices` exécuté après tous les modules et avant `Build`, avec la même sémantique en prod et en test.

**W6. Pas de règle d'identité : les doublons sont traités différemment selon la méthode.**
- **Preuve :**
  - `Add<T>`/`Add(instance)` ne dédupliquent jamais (`AppModuleCollection.cs:19-36`).
  - `AddFromAssembly` déduplique par type exact (`:94`), y compris contre les entrées dont le prédicat est faux : `AddIf<X>(_ => false).AddFromAssembly(asm)` fait que X ne tourne jamais.
  - `Replace` utilise `is TOld` (`:115`), qui accepte aussi les sous-classes.
  - Reproduit : `Add<A>().Add<A>()` provoque `AmbiguousMatchException` **à la requête**. `Add<A>().Replace<A,B>().AddFromAssembly(asm)` fait revenir A.
- **Impact :** middlewares, hosted services ou routes doublés, qu'on ne découvre qu'à l'exécution. `Replace` ne tient pas si un scan suit.
- **Direction v3 [breaking] :**
  - le type devient l'identité, avec une seule politique pour tous les points d'entrée : erreur sur doublon par défaut, multi-instance seulement si on l'active explicitement ;
  - `Replace` devient une substitution par type que les scans suivants respectent ;
  - l'ensemble est validé avant le build.

**W7. La découverte est trop large, fragile et silencieuse.**
- **Preuve :**
  - `IsAppModule` (`ModuleDiscovery.cs:39-44`) ne vérifie pas la visibilité : une classe `private` imbriquée est découverte (reproduit).
  - Les instances sont créées (`:24`) **avant** la déduplication (`AppModuleCollection.cs:94`) : 3 constructeurs appelés pour 2 modules retenus.
  - Les types génériques ouverts ne sont pas exclus : `ArgumentException`, qui fait planter `Start` pour tout l'assembly (reproduit).
  - Les modules avec paramètres de constructeur sont ignorés sans diagnostic. C'est documenté (README:229), mais rien ne s'affiche.
  - Le tri se fait par `Name` et non par `FullName` (`:23`). Renommer une classe réordonne les middlewares.
  - `DiscoverModules` est une requête LINQ paresseuse : chaque énumération recrée des instances.
- **Impact :** enregistrements surprenants, crash au démarrage, effets de bord du constructeur répétés.
- **Direction v3 :**
  - découverte opt-in uniquement (voir W2) ;
  - exclure les génériques ouverts et les types `private` imbriqués, mais **pas** les types `internal`, que beaucoup d'apps utilisent ;
  - filtrer sur les types avant d'instancier ;
  - trier par `FullName` ;
  - ajouter un attribut d'exclusion ;
  - rendre `ModuleDiscovery` interne `[breaking]`.

**W8. Sous AOT ou trimming, la découverte perd des modules.**
- **Preuve :**
  - IL2067 (`ModuleDiscovery.cs:24`), IL2026 (`:31`) et IL2070 (`:43`) apparaissent quand on active `IsAotCompatible`.
  - Avec un publish AOT, un module jamais référencé dans le code renvoie 404. La seule alerte côté consommateur est un IL2104 global sur l'assembly.
  - Le projet ne déclare ni `IsTrimmable` ni `IsAotCompatible`, et n'a aucune annotation.
- **Nuance :** le chemin explicite est sûr (F6), et l'AOT ASP.NET reste un scénario minoritaire.
- **Direction v3 :**
  - `[RequiresUnreferencedCode]` sur `AddFromAssembly*` et sur une surcharge de découverte séparée `[breaking]` ;
  - `IsAotCompatible=true` dans le csproj ;
  - un smoke test AOT en CI ;
  - un générateur de sources reste optionnel : sa justification est l'AOT, pas la vitesse, puisqu'un scan coûte quelques ms.

**W9. `Microsoft.AspNetCore.TestHost` est livré en production.**
- **Preuve :**
  - `TheAppManager.csproj:36-38`, sans `PrivateAssets`. Le nuspec la déclare comme dépendance runtime.
  - Une application consommatrice publie `Microsoft.AspNetCore.TestHost.dll` (84 Ko).
  - Seul `Testing/AppModuleTestHost.cs` l'utilise.
  - Le README le reconnaît (README:87) et la séparation est déjà sur la roadmap (README:398).
- **Impact :** problème de layering. Tests et cœur sont versionnés ensemble, alors que la v3 doit justement faire évoluer les tests. L'impact sur la taille et la sécurité est faible.
- **Direction v3 [breaking] :** créer un paquet `TheAppManager.Testing` branché sur le **seam public** de W1, et non sur `InternalsVisibleTo`, qui forcerait les deux paquets à des versions identiques.

**W10. Deux façades recopient `AppModuleCollection` à la main.**
- **Preuve :**
  - `AppModuleTestHost.cs:24-68` est fait de forwarders d'une ligne.
  - La liste `ConfigureBuilder` est dupliquée (`AppModuleTestHost.cs:17,73-78` contre `AppManagerBuilder.cs:13,31-39`).
  - La politique sur null diverge : ignoré à `AppManagerBuilder.cs:33`, exception à `AppModuleTestHost.cs:75`.
  - #60 et #61 ont dû être implémentés deux fois.
- **Impact :** dérive des comportements et API plus large. Le coût de maintenance reste faible, environ 45 lignes.
- **Direction v3 [breaking] :** un seul objet de composition consommé par tous les hôtes, les forwarders supprimés, et une exception sur null partout.

**W11. Rien n'est observable : la bibliothèque ne journalise rien, et la collection énumérée ne montre pas ce qui tournera vraiment.**
- **Preuve :**
  - Aucun `ILogger`, `EventSource` ou `ActivitySource` dans `src/` (grep).
  - L'énumérateur public ignore les prédicats (`AppModuleCollection.cs:140`). Le test `AddIf_Predicate_StillEnumerated` (`AppModuleCollectionTests.cs:144-147`) fige ce comportement.
- **Impact :** pour savoir si `FeatureX` a tourné en Staging, il faut reproduire. C'est la cause commune des échecs silencieux de W2, W8 et W6.
- **Direction v3 :** après `Build()`, un log Information via `app.Logger` qui liste les modules résolus dans l'ordre et marque ceux qui ont été écartés, plus un timing par phase en Debug. Optionnellement, une API `Resolve(builder)` qui retourne le plan effectif.

**W12. Une erreur dans un module ne déclenche aucun nettoyage.**
- **Preuve :**
  - `ModulePipeline.cs:22-32` n'a pas de try/catch.
  - `_app` n'est assigné qu'après `Build` (`AppModuleTestHost.cs:93`).
  - Reproduit : un module résout un singleton disposable puis lève dans `ConfigureMiddleware` ; après `DisposeAsync`, le singleton n'a jamais été disposé.
- **Nuance :** le nom du module figure déjà dans la stack trace (critique, expérience A).
- **Impact :** fuites de providers, de file watchers et de singletons entre tests d'un même processus.
- **Direction v3 :** disposer l'application si une phase post-build échoue. Encapsuler l'erreur dans une `ModuleConfigurationException` (module, phase) est un plus, pas une priorité.

**W13. La contrainte `new()` est appliquée de façon incohérente.**
- **Preuve :**
  - `new()` est requis sur `Add<T>`, `AddIf<T>`, `Replace<,>` (`AppModuleCollection.cs:19,44,62,113`) et sur les forwarders du test host.
  - `Add(IAppModule)` (`:31`) accepte déjà `new CorsModule(origins)`, et l'initialiseur de collection fonctionne.
  - Il manque `AddIf` par instance et `Replace` par instance ou factory, ce qui empêche de remplacer un mock déjà configuré.
- **Impact :** API incohérente pour les modules paramétrés et pour les doubles de test.
- **Direction v3 :** des surcharges par instance ou factory pour `Replace`/`AddIf`. Pas de résolution des modules depuis la DI, puisque le conteneur n'existe pas encore à ce moment. Pas de `Add<T,TOptions>` sans demande réelle.

**W14. Le cycle de vie en trois phases n'est pas documenté, et le README suggère le contraire.**
- **Preuve :**
  - README:56,73,78,193 ne parlent que d'« ordre d'enregistrement ».
  - Le commentaire `// endpoints last` (README ~199-201) et le diagramme (README:43-63) laissent penser que chaque module s'exécute séquentiellement de bout en bout.
- **Impact :** l'utilisateur ne peut pas raisonner sur l'ordre des middlewares entre modules. C'est pourtant la question principale d'un système de modules, et la principale question de sécurité : un `AuthModule` mal ordonné laisse passer les requêtes.
- **Direction v3 :** une section « Lifecycle » avec un schéma des phases, une phrase « enregistrer d'abord les modules d'exception et d'auth », et un diagramme corrigé.

**W15. L'hôte ne peut pas monter des modules sous un groupe ou une politique commune** (critique).
- **Preuve :** `ModulePipeline.cs:31` passe la `WebApplication` racine à chaque `ConfigureEndpoints`. Impossible d'écrire l'équivalent de `app.MapGroup("/api").RequireAuthorization()` autour d'un ensemble de modules.
- **Impact :** le préfixe, l'auth et les tags OpenAPI doivent être répétés dans chaque module. C'est bloquant pour un module distribué en bibliothèque, qui ignore le préfixe choisi par l'hôte.
- **Direction v3 :** cela vient avec W1. `MapModules(this IEndpointRouteBuilder target, …)` permet d'écrire `app.MapGroup("/api").RequireAuthorization().MapModules(modules)`.

**W16. La suite de tests est fragile et ne couvre pas les chemins les plus risqués.**
- **Preuve :**
  - `tests/.../AppManagerTests.cs:35-78` démarre Kestrel sur le port 5000 par défaut. Seule la ligne 25 utilise le port 0. Aucun `WaitAsync` n'est en place, donc une régression bloque la suite au lieu de la faire échouer.
  - Pas de test de la découverte implicite, pas de test WAF, pas de test d'ordre de découverte.
  - Les `WebApplication` construites ne sont jamais disposées.
  - `TestHost_Replace_KeepsPosition` ne vérifie pas réellement la position (`ReplaceModuleTests.cs:108-122`).
  - `DisposeAsync_CleansUp` n'a aucune assertion (`AppModuleTestHostTests.cs:61-68`).
- **Impact :** les fonctionnalités mises en avant (zéro config, testabilité) sont justement celles qui ne sont pas testées. Le bug W2 est passé inaperçu à cause de ça.
- **Direction v3 :** `--urls http://127.0.0.1:0` et `WaitAsync` partout ; un projet `TestApp` avec des tests WAF (explicite, découverte, override, environnement) ; un test de parité prod/test host.

**W17. Snippet README qui ne compile pas, et `AddIf(bool)` qui piège.**
- **Preuve :**
  - README:210 utilise `builder`, inexistant à cet endroit (CS0103, reproduit). Le README corrige deux lignes plus bas avec le prédicat.
  - `AddIf(bool)` (`AppModuleCollection.cs:44-52`) est évalué avant que le builder existe (`AppManager.cs:69` passe avant `:70`). Il fait double emploi avec le prédicat.
  - Il n'ajoute rien quand la valeur est `false`, si bien qu'un `Replace` ultérieur lève une exception.
- **Direction v3 [breaking] :** supprimer `AddIf(bool)` (`_ => cond` le remplace), et compiler les snippets du README en CI.

### Sévérité basse

| # | Faiblesse | Preuve | Direction v3 |
|---|---|---|---|
| W18 | Interface couplée à `WebApplicationBuilder` plutôt qu'à `IHostApplicationBuilder` | `IAppModule.cs:18`, `AppModuleCollection.cs:62`. Le paquet se décrit comme ASP.NET Core (csproj:12). | Si le scope s'élargit : `ConfigureServices(IHostApplicationBuilder)`, ce qui est peu coûteux `[breaking]`. Attention, on perd `builder.WebHost`. Pas de découpage `IWebModule` sans consommateur réel. |
| W19 | Builder et test host à usage unique, mais rien n'empêche de les réutiliser | Un second `Build()` relance les callbacks puis lève « service collection read-only ». Un second `StartAsync` laisse fuiter la première app, et le client mis en cache (`AppModuleTestHost.cs:107`) pointe toujours vers elle. | Un flag d'état qui lève une `InvalidOperationException` claire. |
| W20 | Les méthodes d'interface par défaut rendent les fautes de frappe silencieuses | `ConfigureService` (sans s) compile et ne s'exécute jamais (reproduit). | Un analyseur Roslyn, non breaking. Pas de classe de base abstraite, qui serait un second concept. |
| W21 | La collection est incomplète : pas de Count, Remove ni Contains ; `AddFromAssemblyOf<T>` exige `T : IAppModule` (`:75`) ; `GetModules()` n'est utilisé que par les tests (`:126`) | Code | Lever la contrainte sur `T` et supprimer l'overload réservé aux tests. Remove et Insert seulement s'il y a une demande. |
| W22 | Pas de métadonnées ni de `DependsOn` | `IAppModule.cs`, roadmap README:393-397 | Seulement si la demande existe : validation sans réordonnancement. |
| W23 | Forme du paquet : 3 namespaces pour 6 types, classes non scellées sans membres virtuels | sample `Program.cs:1-2`, `AppModuleCollection.cs:10` | Un seul namespace `TheAppManager`, et `sealed` partout `[breaking]`. |
| W24 | Un seul ordre pour toutes les phases, et aucun hook async | `ModulePipeline.cs:17-32`, `IAppModule.cs:18-36` | YAGNI. `IHostedService` et `app.Lifetime` couvrent le besoin. Restreindre les paramètres des hooks reste peu coûteux. |
| W25 | Collisions de services entre modules silencieuses (le dernier gagne) | Expérience E du critique : `IThing` donne TB, sans avertissement | Documenter la convention `TryAdd*`. Ne pas construire de détection. |
| W26 | Instances créées tôt : `AddIf` instancie même quand le prédicat est faux (`:65`), `Replace` jette une instance déjà construite (`:119`) | Code | Stocker `(Type, factory, prédicat)` et instancier au moment du build. Optionnel. |
| W27 | Durée de vie des modules non spécifiée : une instance partagée entre plusieurs hôtes peut contaminer les tests | `AppModuleCollection.cs:31-35` | Une ligne de doc. |
| W28 | Métadonnées et identité incohérentes | RepositoryUrl et ProjectUrl pointent vers phmatray (csproj:14,18), alors que ReleaseNotes et le remote pointent vers Atypical-Consulting (csproj:20). Même incohérence dans SECURITY et CONTRIBUTING. La description GitHub parle encore du « Strategy pattern ». La homepage renvoie 404. | Centraliser les métadonnées dans `Directory.Build.props` avec une seule URL d'organisation. |
| W29 | Historique de versions confus | CHANGELOG.md:69 cite une « v1.x » et un guide de migration qui n'existent pas. Le tag v2.0.0 n'est pas un ancêtre de `dev`, et `main` a 38 commits de retard. | Écrire `docs/migration-v2-v3.md`, corriger l'entrée 2.0.0, archiver `main`. |
| W30 | CONTRIBUTING parle de `main` et ne mentionne pas les Conventional Commits | CONTRIBUTING.md:40,73 ; release-please.yml:9. Pas de protection de branche. | Lint des titres de PR, ruleset sur `dev`. |
| W31 | Pas de garde-fous qualité ou API | Pas de `TreatWarningsAsErrors`, `EnablePackageValidation`, PublicApiAnalyzers ni symboles. Pas de `ContinuousIntegrationBuild`, donc le PDB contient le chemin absolu du runner. ci.yml:18 utilise `10.0.x` au lieu de global.json. | Package validation avec 3.0.0 comme baseline après la sortie, snupkg, `ContinuousIntegrationBuild` en CI. |
| W32 | Le sample se limite au cas facile et montre une mauvaise pratique | `WeatherForecastEndpoints.cs:19` renvoie `e.StackTrace`. `AuthModule.cs:10-18` n'a ni scheme ni authorization. Aucun `AddIf` ni test. | Supprimer la fuite de stack trace, ajouter un vrai scheme et `UseAuthorization`, un test WAF, et le pattern options/`ValidateOnStart`. |
| W33 | Le README sur nuget.org s'affiche mal | Images relatives (README:1,4). Usage et Quick Start sont identiques. | Un README NuGet court avec URLs absolues. |
| W34 | Hygiène | Le template de bug cite .NET 8/9, `.dockerignore` sans Dockerfile, branches mortes, logo de 180 Ko pour un nupkg de 197 Ko. | Ménage, sans priorité. |

---

## 4. Positionnement

| Solution | Périmètre | Ordre | Tests de sous-ensemble | Poids | Adoption (NuGet, 2026-10-18) |
|---|---|---|---|---|---|
| **Extensions / Aspire ServiceDefaults** (`builder.AddX()` / `app.MapX()`) | services, middleware et endpoints, mais dans des méthodes séparées sans lien entre elles | implicite, dans le code | non (WAF démarre tout) | aucun | convention du framework |
| **Carter** (`ICarterModule`) | endpoints seulement | non contrôlé | non | léger | 4,76 M |
| **FastEndpoints** | endpoints (REPR) | — | non | moyen | 17,9 M |
| **Mappers générés** (StaticEndpoints, Immediate.Apis…) | endpoints, AOT | — | non | léger | — |
| **ABP** (`AbpModule`, `[DependsOn]`) | framework complet, phases pre/post | tri topologique | via son infrastructure | lourd | 45,9 M (Volo.Abp.Core) |
| **OrchardCore** (`StartupBase`, manifest) | modules, multi-tenant, features | `Order` numérique | non | lourd | — |
| **TheAppManager v2** | les 3 points de contact dans un seul type | liste explicite | **oui** (`AppModuleTestHost` + `Replace`) | ~580 lignes, 16 Ko | 1 975, aucun dépendant |

Créneau défendable : une composition de features plus structurée que des extensions, beaucoup plus légère qu'ABP ou OrchardCore, et testable par sous-ensemble. La v3 doit **s'intégrer à la convention Aspire** (extensions sur le builder) au lieu de s'y opposer.

---

## 5. Questions ouvertes pour la v3

1. **Utilisateur cible.**
   - Le mainteneur seul ? Les données indiquent 0 dépendant externe. Existe-t-il des applications privées en 2.x ?
   - Des équipes qui ajoutent quelques modules à une minimal API existante ?
   - Des équipes « modular monolith » ?

   La réponse fixe la profondeur à donner à W18, W22 et W24.
2. **Point d'entrée.** Les extensions seules (`AddModules`/`UseModules`), ou bien les extensions plus un sucre `AppManager.RunAsync(args, …)` ?
3. **Modèles d'hébergement.** Web uniquement (`WebApplicationBuilder`), ou `IHostApplicationBuilder` pour la phase services afin de couvrir workers et console ?
4. **Découverte.**
   - Supprimer complètement ?
   - Garder la réflexion opt-in et annotée `[RequiresUnreferencedCode]` ?
   - Passer à un générateur de sources ? Si oui, comment découvrir les modules des assemblies référencés ?
5. **Identité des modules.** Unique par type, avec erreur ou ignorance en cas de doublon ? Le multi-instance est-il un scénario supporté, et seulement sur opt-in ?
6. **Stratégie de test.** Garder `AppModuleTestHost` comme produit à part entière, ou pivoter vers « WAF pour la vraie app, plus une extension `ReplaceModule`/`ConfigureTestServices », avec le test host réservé aux slices ? Comment un test atteint-il la composition de production ?
7. **Seam du paquet Testing.** API publique (`BuildWithModules`), ou `InternalsVisibleTo` avec versions verrouillées entre les deux paquets ?
8. **AOT.** Est-ce un objectif déclaré (`IsAotCompatible`, smoke test CI), ou seulement « le chemin explicite est sûr » ?
9. **Observabilité.** Le plan de modules est-il journalisé en Information par défaut, ou en Debug ?
10. **Endpoints.** Accepter un `IEndpointRouteBuilder` cible (MapGroup côté hôte) ?
11. **Dépendances entre modules.** `DependsOn` en validation seule, ou jamais ? Le tri automatique contredit la promesse « pas de magie ».
12. **Nom et identité.** Garder l'ID `TheAppManager` ? Le renommer coûte presque rien aujourd'hui. Quelle organisation est canonique (Atypical-Consulting ou phmatray) ? Quelle phrase de positionnement adopter ?
13. **Hors périmètre déclaré.** Multi-tenancy, hooks async, ordre par phase : à écrire noir sur blanc dans le README pour éviter de les re-débattre.

---

## 6. Proposition de cap v3

**Thème : « Composer avec `Program.cs`, pas le remplacer ».** Les modules deviennent des extensions sur les types du framework, avec un pipeline public, une composition réutilisable et une exécution observable. Comme personne n'utilise la v2, la v3 est une refonte propre, sans shim de compatibilité.

### Must (bloquant pour 3.0)
1. **Extensions primitives** `builder.AddModules(m => …)`, `app.UseModules()` et `IEndpointRouteBuilder.MapModules(…)` ; pipeline public. Suppression de `AppManagerBuilder` et de l'instance `AppManager`. Au plus un sucre `RunAsync`. (W1, W15) `[breaking]`
2. **Découverte explicite uniquement** : assembly ancrée ; génériques ouverts et types `private` imbriqués exclus ; filtrage avant instanciation ; tri par `FullName` ; exception si zéro module. (W2, W7) `[breaking]`
3. **Paquet `TheAppManager.Testing`** construit sur le seam public :
   - accepte une composition réutilisable (W3) ;
   - `WebApplicationOptions`, Development par défaut et `ValidateScopes` (W4) ;
   - hook `ConfigureTestServices` après les modules (W5) ;
   - helper WAF `ReplaceModule` (W3, W9).

   `[breaking]`
4. **Règle d'identité unique** : le type est l'identité ; erreur sur doublon sauf opt-in ; `Replace` persistant face aux scans. (W6) `[breaking]`
5. **Un seul objet de composition** et une seule politique null ; suppression de `AddIf(bool)`. (W10, W17) `[breaking]`
6. **Log du plan de modules** au build, avec les modules actifs et écartés. (W11)
7. **Disposal en cas d'échec** d'une phase post-build. (W12)
8. **Documentation du cycle de vie**, de la règle d'ordre auth/exception et d'un guide de migration v2 vers v3 (simple tableau de correspondance). (W14, W29)
9. **Tests du dépôt** : port 0, `WaitAsync`, tests WAF (explicite, découverte, override, environnement), test de parité prod/test host. (W16, F9)

### Should
- `[RequiresUnreferencedCode]` sur la réflexion, `IsAotCompatible=true`, smoke test AOT en CI. (W8)
- Surcharges par instance ou factory pour `Replace`/`AddIf`. (W13)
- Un seul namespace, `sealed` partout, `ModuleDiscovery` interne. (W23, W7) `[breaking]`
- Garde d'état sur build et start. (W19)
- Snippets README compilés en CI, métadonnées unifiées, sample corrigé (stack trace, auth, options/`ValidateOnStart`, bundle d'extensions). (W17, W28, W32, F8)
- Package validation à partir de 3.0.0, snupkg, `ContinuousIntegrationBuild`, lint des titres de PR. (W30, W31)

### Could
- Générateur de sources pour la découverte, justifié par l'AOT. (W8)
- `ConfigureServices(IHostApplicationBuilder)`, si la question 3 dit oui. (W18)
- Analyseur Roslyn qui détecte les méthodes `Configure*` orphelines. (W20)
- API `Resolve(builder)` qui retourne le plan effectif. (W11)
- `DependsOn` en validation seule. (W22)

### À ne pas faire (explicitement)
- Résoudre les modules depuis la DI : le conteneur n'existe pas encore.
- Tri topologique automatique, nombres de priorité, ordre par phase.
- Hook options/validation, concept « module de modules », groupes de routes propriétaires : le framework les fournit (F8).
- Classe de base abstraite à côté de l'interface, ou découpage `IWebModule` sans consommateur réel.
- Hooks async, multi-tenancy, détection des collisions de services, sécurité « par défaut » dans la bibliothèque.
- Multi-target net8/net9 : les deux arrivent en fin de support en novembre 2026.
- Shims `[Obsolete]`, bridges ou codefix de migration : il n'y a pas de base installée.

---

## 7. Annexe : affirmations réfutées ou corrigées

- **« La v3 doit multi-cibler net8 et net9 »** : réfuté. Les deux sont en fin de support en novembre 2026 ; net10 est la LTS actuelle.
- **« Aucun module ne peut ajouter du middleware après le mapping des endpoints »** : faux. Minimal hosting ajoute `UseRouting`/`UseEndpoints` implicitement, donc l'ordre des phases n'est pas une limite fonctionnelle.
- **« Le découpage en phases apporte la compatibilité avec le routage implicite »** : mauvaise attribution, ce comportement vient de `WebApplication`. La vraie nécessité du découpage est `ConfigureServices` avant `Build()`.
- **« Les erreurs de démarrage n'indiquent pas quel module a échoué »** : faux. La stack trace contient le type du module.
- **« Le seul override de service possible en test est `Replace` »** : faux. Un module d'override ajouté en dernier fonctionne.
- **« Les modules ne peuvent recevoir ni configuration ni paramètres »** : faux. `builder.Configuration` est accessible, et `Add(IAppModule)` accepte une instance construite avec des arguments.
- **« La compatibilité WAF n'est pas vérifiée »** : levé. WAF fonctionne avec l'enregistrement explicite (testé sur le sample) ; seule la découverte échoue.
- **« La 2.2.0 n'est pas publiée sur NuGet »** : levé. Elle est maintenant indexée dans le flat container.
- **« Filtrer la découverte sur les types publics »** : rejeté. Cela casserait les modules `internal`, qui sont courants.
- **« Un Fake*Module est enregistré en production »** : exagéré. Il faudrait qu'il soit placé dans le projet de l'application ; sous un test runner, l'assembly d'entrée est le test host.
- **« L'exemple multi-assembly du README est cassé »** : exagéré. Le cas du README se place entre assemblies différentes. Le doublon n'apparaît qu'avec un `AddFromAssemblyOf<Program>()` suivi d'un `Add<X>()` du même assembly.
- **« Le titre de PR d'un contributeur externe fixe le bump »** : exagéré. En squash-merge, c'est le mainteneur qui choisit le titre final.
- **« La découverte est robuste »** : déclassé. Le catch de `ReflectionTypeLoadException` est un idiome standard, et le même code plante sur les génériques ouverts.
- **« Le tri par `Name` est entièrement déterministe »** : corrigé. Deux types de même nom dans des namespaces différents suivent l'ordre de `GetTypes`.
- **« Les tests sont de première classe »** : déclassé en « utilisables avec des lacunes » (W3 à W5).
- **« La version de TestHost est une version exacte imposée »** : faux. C'est une version minimale (≥ 10.0.13).
- **« La dépendance TestHost en prod augmente la surface d'attaque »** : exagéré. La DLL reste inerte sans `UseTestServer`, et 84 Ko est négligeable.
- **« Absence d'ADR = faiblesse »** : relativisé. Pour environ 600 lignes et un mainteneur, une section « Design decisions » suffit.
- **« Le scan d'assembly est rapide »** : non revérifié faute de ressources. Les chiffres (5 à 17 ms à froid) sont plausibles, et la conclusion reste valable : le générateur se justifie par l'AOT, pas par la performance.
- **Sévérités abaissées après vérification** : facades (haute → moyenne), contrainte `new()` (haute → moyenne), couplage `WebApplication` (haute → moyenne ou basse), AOT de la découverte (haute → moyenne), TestHost en prod (haute → moyenne), snippet README (haute → moyenne), cycle de vie non documenté (haute → moyenne), double `StartAsync` (moyenne → basse).
---

## 8. Décisions v3 (tranchées le 2026-10-18)

| # | Question | Décision |
|---|---|---|
| Q1 | Utilisateur cible | Équipes minimal API, **apps console (Spectre.Console)**, workers, projets **Aspire**. Plusieurs modèles d'hébergement, pas seulement le web. |
| Q2 | Point d'entrée | Les **extensions** sont la primitive (`AddModules` / `UseModules` / `MapModules`). Un **sucre `AppManager.RunAsync(args, m => …)`** est construit dessus. `AppManagerBuilder` et l'instance `AppManager` sont supprimés. |
| Q3 | Hébergement | **Web + `IHostApplicationBuilder`** : la phase services fonctionne sur tous les hôtes (web, worker, console). |
| Q3b | Forme de l'interface | **`IAppModule`** a `ConfigureServices(IHostApplicationBuilder)`. **`IWebModule : IAppModule`** ajoute `ConfigureMiddleware` / `ConfigureEndpoints`, que seul l'hôte web appelle. |
| Q4 | Découverte | **Opt-in explicite** (`AddFromAssemblyOf<T>()`), annotée `[RequiresUnreferencedCode]`. Elle lève une erreur si elle ne trouve aucun module. La découverte implicite via `GetEntryAssembly` est supprimée. |
| Q5 | Doublons | Le **type sert d'identité** : un doublon lève une **erreur avant `Build()`**. Le multi-instance n'est possible que sur opt-in explicite. `Replace` reste valable même si un scan vient après. |
| Q6 | Stratégie de test | **Les deux outils, avec une liste partagée** : un helper `WebApplicationFactory` (`ReplaceModule`, `ConfigureTestServices`) pour la vraie app, et le test host pour les sous-ensembles. Les deux consomment la même composition nommée. |
| Q7 | Native AOT | **Objectif déclaré** : `IsAotCompatible=true`, réflexion annotée, smoke test AOT en CI. |
| Q8 | Seam du package Testing | Uniquement l'**API publique**, sans `InternalsVisibleTo`, pour que les deux packages puissent évoluer à des rythmes différents. |
| Q9 | Observabilité | **Log Information** du plan de modules au build (modules actifs dans l'ordre, et ceux écartés), avec le timing par phase en Debug. |
| Q10 | Endpoints | **`MapModules(this IEndpointRouteBuilder target, …)`** : on peut monter les modules sous un `MapGroup` ou une politique commune. |
| Q11 | DependsOn | **Pas en 3.0.** L'ordre d'enregistrement reste la seule règle. |
| Q12 | Nom | On **garde `TheAppManager`**, avec un positionnement mis à jour. |
| Q13 | Organisation | **Atypical-Consulting** pour toutes les métadonnées (csproj, SECURITY, CONTRIBUTING, description GitHub). |
| Q14 | Hors périmètre déclaré | Hooks async · multi-tenancy · ordre par phase · modules résolus depuis la DI. |
| Q15 | Frameworks cibles | **net10 uniquement.** |

### Conséquences sur le cap v3 (section 6)
- **Must, ajouté :** découpage `IAppModule` / `IWebModule`, et un hôte générique (`IHostApplicationBuilder.AddModules(…)`) testé avec un worker et une app console Spectre.
- **Must, ajouté :** un exemple Aspire (ServiceDefaults + modules) et un exemple console Spectre.
- **Remonté de Should à Must :** AOT (`IsAotCompatible`, annotations, smoke test CI).
- **Promu de Could à Must :** `ConfigureServices(IHostApplicationBuilder)`, qui était W18.
- **Retiré :** `DependsOn`, qui ne figure plus dans Could.
- **README :** une section « Hors périmètre » qui reprend Q14.
