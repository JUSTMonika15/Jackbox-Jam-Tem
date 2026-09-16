# Jam 基础玩法

## 房主权威同步（2026-09-15 晚）

- `GameManager` 现在是场景级 PurrNet `NetworkIdentity`。联网局中只有房主推进 `Waiting → Countdown → Playing → Results`、比赛计时和结算；客户端每秒接收4次时钟快照。进入 MainGame 后，房主等待房间成员的玩家对象全部出现并稳定2秒，再自动开始3秒倒计时，不再要求每端各点一次 Start。
- `PlayerState` 通过房主广播同步现金、圈数、检查点进度、收租、地产数、推人数、显示名和玩家颜色。客户端按住E完成后只发送买地意图；房主再次检查比赛状态、行动状态、范围、归属和余额，再扣钱及广播地产归属。钱、圈数和胜负数据不再由客户端本地决定。
- 地产归属/颜色、金币存在状态、幸运/机遇抽取、监狱/速度/卡片、收租卖地打工和推人命中均由房主决定并广播。玩家移动仍沿用 Starter Template 已验证的 owner-authoritative `NetworkTransform`，没有修改 PurrNet 包、房间、认证、生成器或 transport。
- 联网等待界面不再显示本地 Start 按钮；结算后的 Play Again 只显示给房主。客户端超过2秒收不到房主快照时显示 `Synchronizing with host...`。
- 纯规则回归现为45项全部通过。Unity 主编辑器完成 C# 编译及 PurrNet IL 后处理，日志为 `Tundra build success`，无编译错误。独立无窗口 Play Mode 本轮受 Unity Licensing Client 冲突阻塞，因此仍需要用 Host + Client 两个实例实际确认同一倒计时、买一块地、抽一次事件及同一结算；不要把编译通过表述成跨端实测通过。

## 最新交互与显示

最新排行榜验证：Unity编译无error CS／warning CS，40项规则测试通过。修正出生点后重新实际跑满180秒，自动显示WINNER: Player 1、计分公式、结算排行和Play again；Console零错误零警告。单玩家运行无登录身份，仅验证回退名及画面，不能视作真实房间用户名或多人排行通过。

新增右侧实时排行榜：按总分降序显示玩家颜色、用户名、圈数、现金与总分，并标注Score = Laps x 100 + Cash。结算显示WINNER标题，最高总分相同共享胜利。用户名只读取现有GameOrchestrator房间成员和NetworkIdentity owner对应的认证cookie，不新增RPC或修改联网配置；本地可回退到房间localPlayer／sessionProvider.playerName，无身份时保留Player N。四个Spawnpoint从路边纵列改为南路内部2x2方阵：X=-7/-5.6，Z=-9.3/-10.7，Y=1.3，间距1.4；模型初始朝世界+X跑圈方向。普通Play Mode已确认出生和实时榜画面，用户名真实房间读取尚未进行跨端实测。

玩家临时形象改为Graphics/PlayerBody的小Cube，注册身份时使用与地产相同的PlayerColor材质属性块上色，保留前方朝向标记与原碰撞、移动和联网组件。Unity导入编译成功，普通Play Mode已确认彩色Cube显示。实际等待180秒比赛自然结束，状态自动转为Results、时间0，屏幕显示排名、总分、圈数、现金及Play again按钮，Console零错误零警告；本次仅一名玩家，未验证多人排序或跨端一致性。实时排行榜与显式赢家标题尚未加入。

买地改为在地块内连续按住E／现有Interact两秒，完成才扣钱。松开、完整离开、钱不足、地块已被其他人买下会清空进度；被推中会打断，必须松开后再按。底部屏幕UI显示名称、买价、租金、进度；顶部显示自己的颜色、钱、圈数、积分、时间与按键。等待和倒计时阶段只在本人的小人上方显示浮动YOU箭头，Playing隐藏。

GameManager运行时自动加BoardCamera，使用场景Main Camera的固定高空略倾斜正交视角，暂时停用该相机CinemachineBrain，不修改玩家Prefab的网络组件。镜头不跟随玩家，按画面比例保留上下HUD区域；此地图运行时关闭雾以免高空视角发白，退出时恢复。无需额外Inspector接线。确定性规则测试目前40项通过，包含被推离地块后持续按E不能绕过打断。

- GameManager：场景一个，等待 → 3 秒倒计时 → 默认180秒 → 结算，时间可在 Inspector 调整。
- PlayerState：每个玩家一个，初始 $50，每圈 +$20；Score = 圈数 × 100 + 钱。
- PropertyZone：名称、购买价、租金可调整；在范围内按 Interact（E／手柄北按钮）购买，区域显示归属色。离开后再次进入才重新收租，钱不足不产生负债。
- CoinPickup：默认 +$5，10秒后恢复；比赛结束停止得分、买地和移动。
- GameHud：临时 HUD、购买反馈、排名、重新开局；不替换原房间 UI。

打开 MainGame，在 Unity 执行 `Game Jam > Setup Basic Gameplay`，会为原玩家 prefab 接好 PlayerState、PlayerKnockback、PushAbility 和 PlayerEffects，并保存当前场景。基础区域位于 JamCore_Demo，默认12块地产、14个金币；事件与监狱位于 JamEvents_Demo。不修改道路/检查点，再次执行不重复生成。

地产没有数量上限。复制 Property 对象并调整位置、BoxCollider、Name、Price、Toll 就可以继续增加。道具、全局随机事件、美术和音效是下一层。

地产数值已写入 MainGame 场景：按有效跑圈方向南路 → 东路 → 北路 → 西路依次为买价$20/$40/$60/$90，租金$2/$4/$6/$9，每条路3块。Setup新生成地产也使用此规则。金币仍为14个固定位置、每枚+$5、拾取后10秒原位置刷新；没有随机位置刷新。开局$50、每圈+$20保持不变。

推人已加入玩家 Prefab：PushAbility 读取左键（优先 Push action，否则复用现有 Attack action，手柄西按钮），朝最后移动方向检测，默认范围2.2、力度12、冷却3秒；墙阻挡推击。PlayerKnockback 通过 CharacterController.Move 处理击退并逐步减速。Graphics 转向，玩家根节点和跟随相机不随转向旋转。重开清除冷却和击退。不含伤害、击杀或复活，也未新增网络同步。

Space／现有 Jump action 为落地跳跃，默认高度1.5，沿用当前重力。幸运与机遇格高触发体覆盖跳跃，每组5种结果，完整离开后再次进入可重抽，不是每帧抽。钱仍参与计分；反复抽钱可能刷分，这是目前保留的玩法取舍。

幸运：奖金+$30、账单最多扣现有余额$20、生日其他每人最多给现有余额$5、每块已有地产+$5、自动出狱卡。机遇：返回起点、进监狱5秒、加速1.5倍5秒、减速0.6倍3秒、一次免租卡。两种卡各最多储存1张，加减速替换不叠加。只有交租现金不足会按购买价从低到高半价自动卖地，够付就停止并保留找零；全部卖完仍不够就付出现有余额，剩余免除，原地打工3秒后一次性领$10，期间禁止移动、跳跃、推和买地。生日与账单只扣现金，不卖地、不触发打工。没钱不淘汰、不负债，买地余额不足直接拒绝。

最新验证：40项真实C#规则测试通过；Unity最终编译成功，无error CS／warning CS。Play Mode检查通过实际输入两秒购买、推中取消及离开重进仍须松键、卖地找零、破产停三秒后一次性领工资，以及相机固定与箭头状态切换。最后重新进入普通等待界面，确认白色HUD、本人YOU箭头和完整棋盘画面，Console为零错误、零警告。未做Build、跨客户端同步验证或完整180秒比赛验证。

JamEvents_Demo 包含两个可复制事件格、右路低矮 JailTrap 和内侧 JailPen。陷阱检查脚底高度，跳过可避开；机遇抽中坐牢直接传送。关押时禁止移动、跳跃、推、购买、捡币，已有地产仍可收租；5秒后送到道路出口并免疫坐牢2秒。传送保留已有圈数、重置当前检查点进度，不奖励免费圈。GameManager 的 RaceStart/JailPosition/JailExit 可在 Inspector 调整。

从原主菜单／房间进入游戏，点击 Start match，WASD移动、E购买。Play again 重置钱、圈数、地产和金币。

按用户要求，不改现有网络组件，这些基础脚本为普通 MonoBehaviour／普通变量。确定性规则由独立 C# 测试覆盖；Unity 编译、场景触发与 UI 使用需另外验证，不能把规则测试等同于完整运行通过。

## 本次验证记录（2026-09-15）

- 最终29项真实 C# 规则测试通过（原19项+计分、跳跃公式、刑期/免疫/卡片/速度重置、事件重复进入和经济转账）。先确认新规则缺失时失败，再加入实现并验证通过。
- Unity Editor 最终脚本编译成功（Logs/Editor.log 中 Tundra build success），该轮无 error CS／warning CS。
- 已通过 Editor 菜单保存 MainGame，确认12块地产、14个金币、一个 GameHud 和原玩家 prefab 上的 PlayerState；全部新增脚本的 meta 由 Unity 生成。
- 原道路、检查点、房间与网络组件没有被替换；已有 Testing 目录改动属于之前工作，本次没有 Build。
- Unity 自动在 ProjectSettings/URPProjectSettings.asset 添加 m_ProjectSettingFolderPath: URPDefaultResources；未主动改变渲染配置。
- 先前 Play Mode 操作被自动审核拦截，没有绕过。用户随后明确授权进入 Play Mode：已观察到本地玩家、HUD、Start match 三秒倒计时、比赛计时和地产购买提示。注入 E 按键未观察到购买成功，不能声称购买输入已验证。
- 新增 Editor 菜单 Game Jam > Run Play Mode Smoke Test，用临时目标检查购买、租金、金币、圈数、推击和击退；本次调用因本地玩家已不在场而被前置检查中止（日志 The smoke test needs a local player in Playing state）。这些运行集成测试尚未通过，未排查或改动现有联网行为。
- 推人脚本的 Unity 编译成功，无该轮 error CS / warning CS；已确认 Prefab 配置范围2.2、力度12、冷却3、减速24。运行验证结束后退出 Play Mode，不保存临时测试角色或比赛状态。
- 后续新脚本编译曾因Unity6.6禁用GetInstanceID失败，已改成直接记录碰撞体引用；最终 Unity 脚本编译成功，该轮无 error CS / warning CS。三种新运行脚本的 meta 由 Unity 生成。事件格初始低于道路表面，已抬至y0.32；原道路上表面y0.25，未改道路。
- 后续实际执行 Game Jam > Start Event Runtime Check 两次，通过16项集成断言：奖金/账单修改钱包与分数、自动出狱卡、返回起点、加减速、免租卡消费、关押传送/禁止操作、5秒自动出狱、输入系统排入Space事件后角色实际向上Move，且不触发推的冷却。这不是硬件按键或跨客户端测试。
- 第二次串行执行 JamGameplaySmoke，通过购买扣款/重复购买、真实处理器多碰撞体收租与完整退出、金币只奖励一次、圈数顺序、推击命中去重与冷却，并确认临时目标的 CharacterController 实际产生击退位移。触发处理器被测试工具显式调用；不等同于自动跑完地图触发、按E购买或网络同步验证。退出Play Mode移除临时目标和测试经济状态。
- 尚未验证实际跑跳跨过JailTrap、左键硬件输入、多人生日转账/地产归属/监狱/击退跨客户端一致性或完整180秒比赛。现有联网组件未改，不因单场景测试通过而声明这些状态已网络同步。
- 监狱格回归（2026-09-15）：发现临时角色外观的Sphere/Cube仍带实体Collider，会与根CharacterController自碰撞；运行时现只关闭Graphics子级Collider。Unity 6.6下该CharacterController组合还会停在无Rigidbody Trigger边缘却不派发Trigger回调，因此JailZone改用BoxCollider作为固定关卡范围、关闭其物理参与，并检查GameManager的最多4名玩家是否进入范围。临时项目副本的无窗口Play Mode已通过：CharacterController分步走入红格、传送入狱、禁止行动/推人、5秒后送到出口；Unity脚本编译成功。未验证真人按键跳过陷阱或多人同步。
- 规则测试位于 `C:/Users/10279/Documents/Codex/2026-09-13/new-chat/JamValidation`；运行 `dotnet run --project JamValidation/JamValidation.csproj --no-restore`。
