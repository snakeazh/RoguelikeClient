# Client

Unity 客户端框架工程，从 `cardClient` 抽出的通用部分，**不含原项目的业务模块**；
另带一个可运行的类幸存者战斗骨架（`Assets/App/Battle`，占位美术）作为用法示例。
Unity 版本：`2022.3.62f3`（见 `ProjectSettings/ProjectVersion.txt`）。

打开方式：Unity Hub → Add → 选择本目录 → 打开 `Assets/Scenes/Main.unity` → Play。
首次打开会自动导入资源，之后可用菜单重新生成 Demo 产物：

| 菜单 | 作用 |
|------|------|
| `Tools/Client/Rebuild Demo Assets` | 重建动态字体、`Assets/Res/UI/Home.prefab`、三个战斗预制体与 `Assets/Scenes/Main.unity` |
| `Tools/Client/Check Project` | 校验框架必需资源、预制体丢脚本 |
| `Tools/Client/Smoke Test (Play Mode)` | Play 模式跑一遍启动 + Toast + 对话框，自动退出并给退出码 |
| `Tools/Client/Battle Smoke Test (Play Mode)` | Play 模式自动打一局（4 倍速 40 秒），校验刷怪/击杀/升级，自动退出并给退出码 |

---

## 1. 目录结构

| 路径 | 内容 |
|------|------|
| `Assets/Framework/Assets` | 资源框架：Editor 直读 / 运行时 AssetBundle，`ResourceFramework`、`IResourceService` |
| `Assets/Framework/Log` | `AppLog` + `LogChannel` 分级日志，可选文件落盘 |
| `Assets/Framework/Save` | `ISaveService`，Editor/PC = PlayerPrefs，微信小游戏编译开关下自动切 `WeChatSaveService` |
| `Assets/Framework/UI` | UI 核心：`UIFramework`、`UIManager`、`UINavigator`、`ScreenRegistry`、`ViewBase/ViewModelBase`、`Binding`、`DI`、`Dialog`、`List`、`Navigation` |
| `Assets/Framework/框架使用文档.md` | 框架原始文档，改框架前先读 |
| `Assets/App` | 业务地基：启动、服务容器、音频、网络、配置表、Toast、对话框、通用特效、编辑器工具 |
| `Assets/App/Battle` | 类幸存者战斗骨架：主循环、玩家/敌人/武器/弹体/拾取、升级三选一，见 `战斗系统.md` |
| `Assets/Res` | 运行期资源，**Key = 相对 `Assets/Res` 的无扩展名路径**，bundle 名取第一段小写 |
| `Assets/Plugins/Demigiant` | DOTween（弹窗动画依赖它） |
| `Assets/TextMesh Pro` | TMP 本体与字体，界面文案统一用 `TextMeshProUGUI` |
| `Assets/Res/Fonts/DemoFont SDF.asset` | 动态 TMP 字体（由 `GameFont.otf` 生成，已设为 TMP 全局默认字体） |
| `Assets/StreamingAssets/Bundles` | 打包产出的 AssetBundle 输出目录 |

`Assets/Res/UI` 只保留框架与战斗骨架用到的界面：`UIRoot.prefab`（层根）、`ConfirmDialog.prefab`、
`Popup/ToastPanel.prefab`、`Home.prefab`（Demo 首页），以及 `Battle/BattleHud.prefab`、
`Battle/LevelUp.prefab`、`Battle/BattleResult.prefab`（战斗 HUD / 升级三选一 / 结算）。

依赖（`Packages/manifest.json`）只保留 Unity 官方包 + Newtonsoft + TMP + Timeline + Cinemachine + VisualScripting，
**没有微信 SDK**。`Framework/Save/WeChatSaveService.cs` 只用 `DllImport` 声明微信接口，缺 SDK 也能编译，
非微信平台不会走到那段代码。

## 2. 启动流程

入口 `Assets/App/Bootstrap/AppBootstrap.cs`，顺序固定：

```
AppServices.Create()                    // DontDestroyOnLoad 的服务容器
ResourceFramework.Create() + InitializeAsync()
SaveFramework.Create()
AudioService(save, host).Load() + PreloadUiClickAsync()
HttpApiClient(save)
ConfigTables.LoadAsync(resources)       // ViewModel 构造函数常直接读表，必须早于 UI
resources.LoadAsync<GameObject>("UI/UIRoot")   // UIFramework.Create 要求已在缓存里
UIFramework.Create(container)           // 注册 UIRoot / Navigator / DialogService，扫描 [AutoScreen]
ToastService
ui.Open(HomeViewModel)
```

新增业务服务注册在 `ConfigTables` 之后、`UIFramework.Create` 之前；`UIFramework.Create` 之后
容器里才有 `IUIManager` / `IUINavigator` / `IDialogService`。

任何一步抛异常都会 `AppLog.Exception` 并停在启动场景，方便定位，不会带着半个容器进首页。

## 3. 加一个界面（四步）

1. `App/UI/<模块>/XxxViewModel.cs`：继承 `ViewModelBase`，状态用 `ObservableProperty<T>`，
   操作暴露 `IRelayCommand`，生命周期重写 `OnOpen/OnHide/OnClose`。
2. `App/UI/<模块>/XxxView.cs`：`[AutoScreen(AppScreenIds.Xxx, UILayer.Page, ResResourcePaths.Xxx)]`
   + `class XxxView : ViewBase<XxxViewModel>`，`OnBind()` 里 `Binding.BindText/BindCommand/...`。
3. 预制体放 `Assets/Res/UI/Xxx.prefab`：根节点挂 `XxxView` + `UIReference`，
   需要绑定的节点挂 `UIBind`（key 默认取节点名），用 `UIReference` 的右键菜单 `Collect UIBinds` 收集。
   **绑 Button 时必须把 `UIBind.Target` 显式指到 Button 组件**，默认会拿到 Image。
4. 在 `AppScreenIds`、`ResResourcePaths` 加常量，`ui.Open(container.Resolve<XxxViewModel>())` 打开。

`Assets/App/UI/Home` + `Assets/Res/UI/Home.prefab` 是完整可运行的样例：文本绑定、命令绑定、
`IDialogService.ConfirmAsync`、`ToastService`、配置表读取都在里面。

### 字体

原工程的 `Assets/TextMesh Pro/Fonts/GameFont SDF.asset` 是 **Static** 字体图集（只含导出时的
Lang-zh 子集），中文缺字会静默变空格。本工程由 `Tools/Client/Rebuild Demo Assets` 从
`GameFont.otf` 生成 **动态** 字体 `Assets/Res/Fonts/DemoFont SDF.asset`（`AtlasPopulationMode.Dynamic`，
2048×2048，按需加字形），并把它设为 TMP 全局默认字体（`Assets/TextMesh Pro/Resources/TMP Settings.asset`
的 `m_defaultFontAsset`），同时把 `UIRoot` / `ConfirmDialog` 里原有的 GameFont 引用换掉。

新增界面一般不用管字体：不显式设 `font` 的 TMP 都走全局默认字体。若确实要换字体，
把 `TMP_Text.font` 指到 `Assets/Res/Fonts/DemoFont SDF.asset` 即可，**不要**再指回静态的 `GameFont SDF`。

## 4. 配置表

- json 放 `Assets/Res/Config/*.json`，顶层是数组；
- 表类继承 `ConfigRowBase<T>`（按 Id 索引）或 `ConfigConstBase<T>`（单例常量）；
- 在 `App/Config/ConfigTables.LoadAsync` 里加一行 `LoadArrayAsync<T>`；
- 示例：`DemoConfig.json` + `DemoItemConfig`（`DemoItemConfig.All` / `Get(id)` / `Count`）。

新增一张表不要改框架，照抄这三处即可。

## 5. 网络

`App/Net/HttpApiClient`：Bearer token 存 `ISaveService`（key `net.auth.token.v1`）、
camelCase JSON、401 自动清 token、超时 15s、失败抛 `GameApiException(code, message, status)`。
服务器地址在 `App/Net/GameApiSettings`（LocalHost / Custom / RemoteDefault 三档），
启动时由 `AppBootstrap` 的 Inspector 字段覆盖。业务端点请各自封装成 `XxxApi`，不要往 `HttpApiClient` 里塞业务。

## 6. 其它通用设施

| 设施 | 用法 |
|------|------|
| 音频 | `IAudioService`：`PlayBgm` / `PlaySfx` / `PlayUiClick`，开关状态自动保存 |
| Toast | `Toast.Show/Success/Warning/Error(msg)`，服务未注册时静默忽略 |
| 对话框 | `IUIManager.Dialogs.ConfirmAsync(title, message, DialogButtons.OkCancel)` |
| 存档 | `ISaveService`；实现了 `ISaveFlushable` 的服务会被 `AppServicesHost` 在暂停/退出时自动 `Save()` |
| 日志 | `AppLog.Info(LogChannel.Xxx, msg)` |
| 列表 | `RecycleListView`（仅垂直、item 高度固定） |
| 特效工具 | `App/UI/Effects` 下的通用 Tween/遮罩/适配组件 |

## 7. 战斗骨架（类幸存者）

首页「开始战斗」进入，单局 5 分钟、竖屏单手、武器自动索敌。完整说明见
`Assets/App/Battle/战斗系统.md`，这里只列要点：

- 入口：`BattleLauncher.StartAsync()`（关首页 → 建 `BattleRunner` → 开 `BattleHud`）；结算后自动回首页。
- 角色：首页两张卡片选人（游侠 90 血 / 5.4 速 / ★长弓，剑士 125 血 / 4.6 速 / ★大剑），
  选择存 `battle.character.selected.v1`；角色自带的兵器不占武器槽，可升级、可进化。
  数值在 `Assets/Res/Config/Battle/Characters.json`，改表即改角色。
- 主循环：`Assets/App/Battle/BattleRunner.cs`；纯数据在 `BattleSession`；升级三选一在 `ChoiceService`。
- 操作：左半屏任意位置按下即出现动态摇杆；右下 260×260 区域点按冲刺（2.5 身位 / 0.2s / 无敌 0.3s / CD 3s）。
- 内容：8 把武器（投射/环绕/追踪/光环/扇形剑气/连锁闪电/定时炸弹）+ 8 把进化武器、
  10 个被动、7 种敌人 + 5 种精英词缀、3 个 Boss（半血二阶段）、经验升级三选一、7 种拾取物。
- 进化：武器满级且配套被动满级后，开宝箱即进化（如「回旋刃 + 大弹弓」→ 千刃轮）。
- 局外记录：`BattleRecords` 存在 `battle.meta.*.v1`（累计金币 / 出战 / 胜利 / 最高击杀 / 最长存活），
  首页会显示这行小结。
- 配置：`Assets/Res/Config/Battle/{Enemies,Weapons,Passives,Waves,Levels,Characters}.json`，
  加武器/敌人/波次/进化/角色都只改 json（+ 新形态才需要加枚举分支），不用动 UI。
- 美术（2D 精灵）：战斗内所有角色都是 SpriteRenderer 精灵，资源在
  `Assets/Res/Battle/Sprites/`、显示用预制体在 `Assets/Res/Battle/Prefabs/`，由 `BattleVisuals` 统一加载；
  精灵缺失时自动退回 `PlaceholderArt` 的纯色图形，所以删掉美术也不会报错。
  重建全部精灵与预制体：菜单 `Tools/Client/Rebuild Battle Sprites`
  （或 `-executeMethod App.EditorTools.BattleSpriteBuilder.RebuildAll`）。
  敌人/武器用哪个精灵由 json 的 `Sprite` 字段指定（`Enemies.json` / `Weapons.json`），
  新增敌人只要把同名 PNG 放进 `Sprites/` 再填字段即可。
  回旋刃是画出来的木纹回旋镖（`blade.png`，V 形两臂 + 木纹 + 刻纹蜥蜴），代码在
  `BattleSpriteBuilder.Blade / PaintLizard`。
  相机是 2D 正交（`orthographicSize = 5`），排序号统一在 `Assets/App/Battle/BattleSort.cs`：
  地面/墙 -100/-98，角色按世界 y 分档（`BattleSort.Character`，越靠下越靠前，同档用实例 id 分子槽），
  拾取物 400、炮台 410+、环绕刃 420、弹体 440、特效 450+、飘字 470。
  角色按 y 排序是为了避免重叠精灵因为相机移动而逐帧交换前后顺序（表现为「怪物闪烁」），
  新增实体时从 `BattleSort` 取号，不要写死数字。
  朝向：贴图统一按「正前方朝上」画，运行期按速度方向（弹体）、移动方向（玩家）、
  指向玩家的方向（敌人）旋转，公式是 `atan2(dir.y, dir.x) - 90°`；新贴图有正前方时必须照此绘制。
- 大剑刀光：大剑（`Kind=4 Cleave`）不再自动索敌，**只朝玩家面向方向**挥出一记 120° 扇形，
  命中范围内的所有敌人并放一道月牙弧（`slash_arc.png` 贴图 + `TransientFx.Fan`，0.26s 张开淡出）。
  扇形角度改 `WeaponRuntime` 的 `CleaveSpread`，弧的粗细/亮度改 `BattleSpriteBuilder.SlashArc`；
  细节见 `Assets/App/Battle/战斗系统.md` 的「2.1 2D 精灵」与第 3 节。
- 地图：战场是 **18 × 36 世界单位**的大地图（一屏只有 5.63 × 10），镜头用 `CameraFollow` 跟玩家滚动、
  到地图边缘停住；地面由 `tile_floor` 平铺、四周是 `wall_h`/`wall_v` 平铺的墙；玩家与刷怪都以
  `BattleMap.Walkable` 为界。改尺寸只动 `Assets/App/Battle/BattleMap.cs` 的三个常量。
  竖屏保护：窗口被拉宽时 `BattleRunner.ApplyAspectGuard` 会把画面限制成 9:16 的中间一列（左右留黑），
  否则可见宽度超过地图宽度，镜头夹取会退化成「永远居中」、看起来像不跟随；桌面包启动时
  `AppBootstrap.ApplyDesktopWindow()` 会强制回到 540×960 竖屏窗口。
- 受击反馈：伤害飘字（`DamagePopup`，暴击金色加 `!`）、敌人闪白 + 挤压 + 击退
  （`EnemyUnit.PlayHitFeedback`）、命中与死亡火花（`TransientFx.Flash`）、玩家闪红、
  屏幕四周泛红（`HurtOverlay`）、镜头震动（`CameraFollow.Shake`）。
  细节与可调常数见 `Assets/App/Battle/战斗系统.md` 的「2.4 受击反馈」。
- 雷电特效：雷电法典（`Kind=5 Chain`）命中时用 `LightningFx.Bolt()` 画抖动电弧
  （`LineRenderer` + 两端光斑，0.15s 淡出），先连玩家→首个敌人，再在敌人之间串联，越靠后越暗；
  天罚法典复用同一套。要调粗细/抖动幅度改 `Assets/App/Battle/LightningFx.cs` 的两个 clamp。
- 随身炮台：`Weapons.json` 里 `Mount = 1` 的武器（魔法炮 / 湮灭炮）会在玩家侧边生成一门炮台
  （`CannonRig`），自动转向最近的敌人（临界阻尼转向，`AngleSmoothTime` / `MaxTurnSpeed` 调手感），
  冷却期间炮口光球由小变大、外圈能量环向炮口收缩，炮口对准之后才开火（后座 + 炮口闪光），
  弹体从炮口而不是玩家中心射出。细节见 战斗系统.md 的「2.5 随身炮台」。
- 未包含：局外成长树、暂停菜单、音效、正式 UI 皮。

## 8. 打包

### 8.1 菜单

| 菜单 | 产物 | 说明 |
| --- | --- | --- |
| `Build/Desktop/Build Windows64 (Rebuild Bundles)` | `Builds/Windows/<产品名>_<版本>_<时间戳>/` | 540×960 竖屏窗口，验证真机同款资源路径 |
| `Build/Android/Build APK (Rebuild Bundles)` | `Builds/Android/*.apk` | 手机安装包 |
| `Build/Android/Build AAB (Rebuild Bundles)` | `Builds/Android/*.aab` | 上架 Google Play |
| `Build/Android/Build APK (Test Logs)` | `Builds/Android/*_TestLogs_*.apk` | 追加 `ENABLE_APP_LOG`，真机上能看到 Info/Debug |

两个平台菜单都会先把 `Assets/Res` 打成 AssetBundle：输出到 `Assets/StreamingAssets/Bundles`，
同时归档到 `Client/Bundles/<版本>/`（版本号按内容哈希递增，内容没变则复用旧版本号）。

### 8.2 命令行

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
$proj  = "E:\Work\Roguelike\Client"

# Windows 播放器（先重建 bundle 再出包）
& $unity -batchmode -quit -projectPath $proj `
  -executeMethod App.Build.Editor.DesktopBuildMenu.BuildWindows64WithBundles -logFile "$proj\Logs\build-win.log"

# Android APK（同上）
& $unity -batchmode -quit -projectPath $proj `
  -executeMethod App.Build.Editor.AndroidBuildMenu.BuildApkWithBundles -logFile "$proj\Logs\build-android.log"
```

### 8.3 注意事项

- **bundle 与平台绑定**：`BuildPipeline.BuildAssetBundles` 按 `BuildTarget` 出包，Windows 播放器加载
  Android 的 bundle 会报 `Failed to load AssetBundle`。两个菜单已分别传 `StandaloneWindows64` / `Android`，
  并在 `StreamingAssets/Bundles/platform.txt` 写平台标记；出包时发现标记不匹配会自动重建（桌面）
  或告警（Android）。手写脚本时不要漏传（`ResAssetBundleBuilder.RebuildCurrentVersion(BuildTarget)`）。
- **签名**：没有自有 keystore 时，在 `Build/Android/Build Settings` 里关掉「使用自定义 Keystore」，
  用 Unity 调试签名出测试包；正式发版再填 `user.keystore` / alias / 两个密码。
- **日志**：发布版 `AppLog.Info/Debug` 会被 `[Conditional]` 裁掉（见 `Assets/Framework/Log/Runtime/AppLog.cs`），
  所以真机 `Player.log` 里看不到 `[Boot]` 之类；排查问题时出 Test Logs 包。
- **资源直读开关**：Editor 下 `EditorPrefs` 的 `Framework.Assets.UseEditorRes` 决定直读 `Assets/Res` 还是走 bundle；
  播放器里恒走 bundle（`ResourceLoadMode.UseEditorRes` 在非 Editor 下返回 false）。
- `Assets/App/Build/Editor` 另有 Android keystore 设置窗口与代码裁剪保护（`EngineCodePreserve`）；
  `Assets/App/Editor` 是资源导入后处理（图集、贴图、Raycast 清理等）。

## 9. 命令行自检（CI / 交接验收）

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
$proj  = "E:\Work\Roguelike\Client"

# 1. 重新生成 Demo 产物（含动态字体）
& $unity -batchmode -quit -projectPath $proj `
  -executeMethod App.EditorTools.DemoPrefabBuilder.RebuildAll -logFile "$proj\Logs\build.log"

# 1b. 重新生成战斗 2D 精灵与预制体（改了 BattleSpriteBuilder 里的画法才需要）
& $unity -batchmode -quit -projectPath $proj `
  -executeMethod App.EditorTools.BattleSpriteBuilder.RebuildAll -logFile "$proj\Logs\sprites.log"

# 2. 校验框架必需资源、预制体丢脚本（退出码 0 = PASS）
& $unity -batchmode -quit -projectPath $proj `
  -executeMethod App.EditorTools.FrameworkSelfCheck.Run -logFile "$proj\Logs\selfcheck.log"

# 3. Play 模式冒烟：启动 → 配置表 → Toast → 对话框，退出码 0 = PASS
& $unity -batchmode -projectPath $proj `
  -executeMethod App.EditorTools.RuntimeSmokeTest.Run -logFile "$proj\Logs\smoke.log"

# 4. Play 模式战斗冒烟：自动打 40 秒，校验刷怪/击杀/升级，退出码 0 = PASS
& $unity -batchmode -projectPath $proj `
  -executeMethod App.EditorTools.BattleSmokeTest.Run -logFile "$proj\Logs\battle-smoke.log"
```

冒烟测试用 `SessionState` 跨 domain reload 保存状态，日志出现 `[RuntimeSmokeTest] PASS` 后自行退出；
不要给它加 `-quit`（Play 期间 `-quit` 不等退出流程走完）。
批处理过程若异常卡住，检查并删除 `Temp/UnityLockfile`。

## 10. 交接清单

- Demo 相关可整体删除：`Assets/App/UI/Home`、`Assets/Res/UI/Home.prefab`、
  `Assets/Scenes/Main.unity`、`Assets/App/Editor/DemoPrefabBuilder.cs`、`Assets/Res/Config/DemoConfig.json`、
  `App/Config/DemoConfig.cs`（以及 `ConfigTables` 里的对应一行）、`App/UI/AppScreenIds.cs` 里的 `Home`。
- 战斗骨架不想要时，可整体删除：`Assets/App/Battle`、`Assets/App/UI/Battle`、
  `Assets/Res/UI/Battle`、`Assets/Res/Config/Battle`、`Assets/Res/Battle`（2D 精灵与预制体）、
  `Assets/App/Editor/BattleSmokeTest.cs`、`Assets/App/Editor/BattleSpriteBuilder.cs`，
  并去掉 `AppBootstrap` 里的 `BattleLauncher` 注册、`HomeViewModel/HomeView` 的 `StartBattleCommand`、
  `AppScreenIds` 里的三个战斗界面 id、`ResResourcePaths` 里的战斗 key、`ConfigTables` 里的战斗表加载。
- 若要换成别的字体，重新生成 `Assets/Res/Fonts/DemoFont SDF.asset` 并同步 TMP 默认字体即可。

## 11. 版本管理（Git）

仓库根目录就是工程根目录（`.git` 与 `Assets` 同级），主分支 `main`。

```powershell
cd E:\Work\Roguelike\Client
git status                # 看这次改了什么
git diff -- Assets/App    # 看代码差异
git add -A
git commit -m "feat: 大剑改成面向方向的扇形刀光"
git log --oneline -10     # 最近的版本
```

约定：

- `.gitignore` 已排除生成物 —— `Library/`、`Temp/`、`Builds/`、`Bundles/`、
  `Assets/StreamingAssets/Bundles/`、`Logs/`、`UserSettings/` 和 Unity 自动生成的
  `*.csproj` / `*.sln`。**这些不要提交**，否则仓库会膨胀到几个 G。
- 提交粒度跟着「一次改动」走：修一个 bug、加一个功能、调一次数值各一次提交；
  提交信息用 `feat:` / `fix:` / `docs:` / `chore:` 前缀，后面写清改了什么。
- `Assets/**/*.meta` 必须一起提交（Unity 用它维持资源引用，漏了会丢引用）。
- 二进制美术资源（`Assets/Res/**/*.png`、字体 `.otf`/`.asset`）直接入库；单张图很小，不需要 LFS。
- 改了二进制资源想回退时用 `git checkout -- <path>` 即可，`.meta` 要一起回退。
