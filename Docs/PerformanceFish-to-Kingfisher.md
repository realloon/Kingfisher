# PerformanceFish to Kingfisher

面向项目：`Kingfisher`  
目标版本：`RimWorld 1.6`

Performance Fish 对 `Kingfisher` 仍然有参考价值，但它现在更适合作为“热点地图和思路来源”，而不是“直接移植清单”。

一个关键前提是：`Kingfisher` 的实现选择不是教条式的 `Prepatcher-first`，而是围绕性能目标和工程约束来选更合适的方案。对 PF 条目的评估，核心问题不是“该不该用 Harmony”，而是“哪种实现方式在这个热点上有更低的额外开销、更稳定的语义和更可控的维护成本”。

这份文档按 `Kingfisher` 当前代码结构维护，不再使用旧的 `Source/Patches/...` 路径表述，也不再把已落地项描述成单纯 Harmony patch。

## 当前实现约定

`Kingfisher` 当前更接近下面这套选型原则：

1. 热路径上如果 `freepatch` / 方法体重写能更直接地表达目标，并且能避开 `prefix/postfix` 包装与额外调用开销，就优先这样做
2. 如果问题本质上是状态缺失或索引缺失，就优先考虑 `Prepatcher` 字段注入或显式缓存
3. 如果窄口 `Harmony` patch 更简单、更稳，或者只是做状态同步 / 入口拦截，就直接用 `Harmony`

补充约定：

4. 如果一个 `Harmony Prefix` 的语义本质上是“完整替换原方法并 `return false`”，默认应当优先改成 `Prepatcher` 方法体重写；只有在确实需要 `Harmony` 的组合能力时才保留 `Prefix`

换句话说，`Prepatcher` 和 `Harmony` 都是工具，关键在于性能收益、语义风险和实现复杂度的权衡，而不是形式上的先后级别。

当前 `Prepatcher` 入口：

- `Source/Prepatching/AssemblyCSharpMethodRewriter.cs`
- `Source/Prepatching/InjectedThingCompFields.cs`

当前已落地功能目录：

- `Source/Features/Combat/`
- `Source/Features/Things/`
- `Source/Features/Buildings/`
- `Source/Features/Thoughts/`

这也决定了我们和 Performance Fish 的取舍不同：更偏向小而硬的等价重写、显式状态和可验证的预注入；在真正的热路径上，会主动考虑 `freepatch` 来减少运行时包装成本；同时仍然避免大范围隐式缓存和行为敏感基础设施改造。

## 已正式落地

### `ListerThings.Remove`

实现方式：

- `Prepatcher` 方法体重写注册：`Source/Prepatching/AssemblyCSharpMethodRewriter.cs`
- 具体实现：`Source/Features/Things/ListerThingsRewrite.cs`

结果：战斗场景下收益明确。

### `ListerBuildings` def 查询

实现方式：

- `Prepatcher` 方法体重写注册：`Source/Prepatching/AssemblyCSharpMethodRewriter.cs`
- `Prepatcher` 字段注入与查询重写：`Source/Features/Buildings/ListerBuildingsRewrite.cs`
- 辅助性 Harmony 同步：`Source/Features/Buildings/ListerBuildingsAddPatch.cs`
- 辅助性 Harmony 同步：`Source/Features/Buildings/ListerBuildingsRemovePatch.cs`

覆盖：

- `ListerBuildings.AllBuildingsColonistOfDef`
- `ListerBuildings.ColonistsHaveBuilding`
- `ListerBuildings.ColonistsHaveBuildingWithPowerOn`

结果：收益有限，但稳定正向。

### `AttackTargetFinder.BestAttackTarget`

实现方式：

- `Prepatcher` 方法体重写注册：`Source/Prepatching/AssemblyCSharpMethodRewriter.cs`
- 具体实现：`Source/Features/Combat/AttackTargetFinderRewrite.cs`

结果：战斗场景下收益明确。

### `PawnDiedOrDownedThoughtsUtility`

实现方式：

- `Prepatcher` 方法体重写注册：`Source/Prepatching/AssemblyCSharpMethodRewriter.cs`
- 主体重写：`Source/Features/Thoughts/PawnDiedOrDownedThoughtsRewrite.cs`
- 仅剩的窄口 Harmony 拦截：`Source/Features/Thoughts/TryGiveDiedThoughtsPatch.cs`

当前覆盖：

- `PawnDiedOrDownedThoughtsUtility.RemoveLostThoughts`
- `PawnDiedOrDownedThoughtsUtility.RemoveResuedRelativeThought`
- `PawnDiedOrDownedThoughtsUtility.TryGiveThoughts(..., Died)`

结果：战斗场景下已实测有效。

## 已做过 profiler，当前测试存档下不成立

### 爆炸相关怀疑路径

涉及：

- `ExplosionCellsToHit`
- `GenSight.LineOfSight` in explosion context
- `Explosion.Tick -> AffectCell -> DamageThing -> TakeDamage -> AddHediff`

结果：我们定位过链路，但没有找到一个低风险、可稳定复现、值得正式落地的切点。  
结论：当前暂缓。

### `ListerHaulables.ListerHaulablesTick`

结果：在高层入口里相对更大，但绝对值仍小。  
结论：还不够支撑正式 patch；继续向下拆的收益证明不足。

### `StoreUtility.NoStorageBlockersIn`

结果：调用频繁，但单次极轻，且 blocker 并不是主要失败原因。  
结论：PF 的 `StorageBlockerGrid` 思路在当前测试存档里没有足够收益空间。

### `HediffSet.GetFirstHediffOfDef` / `HasHediff`

结果：调用很多，但 `avgHediffCount` 只有约 `2`。  
结论：线性扫描成立，但绝对成本太低，不值得在当前场景立项。

### `HediffComp_TendDuration`

涉及：

- `HediffComp_TendDuration.CompPostTick`
- `HediffComp_TendDuration.IsTended`
- `HediffComp_TendDuration.AllowTend`
- `HediffComp_TendDuration.CompTended`
- `HediffComp_TendDuration.CompTipStringExtra`
- `HediffComp_TendDuration.CompDebugString`
- `HediffComp_TendDuration.CompExposeData`

已做过：

- 先用 deep profiler 拆到 injury hotpath，发现 `HediffComp_TendDuration.CompPostTick` 在局部细账里很显眼
- 做过一版 `Harmony Prefix + return false` 的完整替换
- 随后又按“完整替换应优先用 `Prepatcher`”的原则，把整组入口改成了 `Prepatcher` 方法体重写
- 最终都回到 aggregate 总账做 AB

结果：

- deep profiler 的局部热点结论没有转化成 aggregate 总账收益
- 即使切成 `Prepatcher` 重写，补丁版在真实战场存档下仍未体现正收益
- 进一步按调用次数折算后，`HealthTick` / `HealthTickInterval` 的单次成本也没有改善，说明问题不在 `Harmony` 包装，而在这个优化点本身不成立

结论：

- 这条优化在当前测试存档下已证伪，不再继续投入
- deep profiler 只能用于“抽丝剥茧找结构”，不能直接把局部热点当成立项依据
- 正式立项必须回到 aggregate 总账 AB；如果总账不成立，即使局部看起来很热，也不应保留正式补丁

### `Room.ContainedBeds`

结果：实现形态不漂亮，但调用频率和绝对耗时都很低。  
结论：不值得做正式补丁。

## 静态上仍可疑，但目前没有 profiler 证据支撑立项

### `ThingDef.GetCompProperties<T>` / `HasComp*`

已确认：

- `ThingDef.GetCompProperties<T>` 在 1.6 原版中仍然是线性扫描
- `ThingDef.HasComp(Type)` / `ThingDef.HasComp<T>()` 在 1.6 原版中仍然是线性扫描

备注：我们之前在 Harmony generic patch 上踩过工程性问题。  
当前结论：仍可疑，但目前不能写成“优先开发项”。

### `ThingOwner<T>.Remove`

已确认：`ThingOwner<T>.Remove` 在 1.6 原版中仍有双扫描问题。

备注：PF 默认方案带有无序删除语义，不适合直接搬。  
当前结论：仍值得保留怀疑，但还没有实测证据。

## 仍可作为专题参考，但现在不是小 patch 待办

### 通用 work scanning

涉及：

- `JobGiver_Work.TryIssueJobPackage`
- `WorkGiver_Haul.PotentialWorkThingsGlobal`
- `JobGiver_Haul.TryGiveJob`

保留依据：1.6 原版仍然会在工作扫描中反复组合 `PotentialWorkThingsGlobal`、`IsForbidden`、`HasJobOnThing`、`GenClosest` 等判定；hauling 路径也仍然把 `listerHaulables` 结果直接送进后续昂贵检查。

结论：PF 在这里的核心不是单个查询缓存，而是“先做更便宜的筛选 / 排序，再减少后续重检查次数”。这条是正式专题，不应遗漏。

### `WorkGiver_DoBill`

已做过高层入口观察。

结论：这是一个需要单独 profiling 和单独设计补丁的系统，不能仅凭当前证据写成小范围待办。

### `ThingOwnerUtility.GetAllThingsRecursively`

保留依据：1.6 原版仍然使用 `tmpStack` / `tmpHolders` 递归遍历 holder 树，并持续向输出列表 `AddRange`；`WorkGiver_DoBill.TryFindBestIngredientsHelper` 在遍历 haul source 时仍会直接调用它。

结论：这是 `DoBill / ingredient search / holder traversal` 的专题级方向，不应遗漏，但也不能在没有专题 profiler 的情况下直接写成小 patch 待办。

### `RecipeDef.AllRecipeUsers`

保留依据：1.6 原版 `AllRecipeUsers` 在 `recipeUsers == null` 时仍会扫描 `DefDatabase<ThingDef>.AllDefsListForReading`，逐个检查 `recipes.Contains(this)`。

结论：PF 将它作为 unfinished thing / bill 相关路径里的候选缓存点。这条不应缺席，但更适合并入 `DoBill` 专题统一评估。

### `ListerHaulables`

已做过高层入口观察。

结论：这是一个需要单独 profiling 和单独设计补丁的系统，不能仅凭当前证据写成小范围待办。

### `StorageSettings.AllowedToAccept`

保留依据：1.6 原版 `AllowedToAccept(Thing)` / `AllowedToAccept(ThingDef)` 仍然先走 `filter.Allows(...)`，再递归检查 parent storage settings，因此它仍然可能在 hauling / storage 链路里反复出现。

结论：仍值得保留，但应并入 hauling / storage 专题统一看，不适合孤立立项。

### `WorldObjectsHolder.WorldObjectsHolderTick`

保留依据：1.6 原版仍然先把 `worldObjects` 拷贝到 `tmpWorldObjects`，再对结果逐个 `DoTick()`，没有先筛掉明确不需要 tick 的 world object。

结论：PF 在这里做的是 world 子系统级 tick 筛选。这条应保留在雷达里，但需要 world 场景下的专门 profiling 和验证。

### `Pawn_GeneTracker.GeneTrackerTick`

保留依据：1.6 原版仍然分别遍历 `xenogenes` 和 `endogenes`，对所有 `Active` gene 逐个 `Tick()`。

结论：PF 在这里的思路是预先筛出真正需要 tick 的 gene。这不是小查询 patch，而是 Biotech 场景下的专题级方向。

### `DynamicDrawManager.DrawDynamicThings`

保留依据：1.6 原版仍然包含 `ThingCullDetails` 分配、可见性计算、预绘制和正式绘制的整条动态绘制流程。

结论：这是 FPS 侧的重要专题。我们目前主要在看 TPS / gameplay 热点，这条不应被当成“已经覆盖过”的方向。

### `MassUtility.GearMass` / `InventoryMass`

保留依据：1.6 原版 `GearMass` 仍然逐件扫描 apparel 和 equipment，`InventoryMass` 仍然逐个扫描 `innerContainer`，所以在 caravan / carry capacity 场景里仍可能成为候选。

结论：这条可以继续保留，但优先级取决于 caravan / inventory 相关 profiler。

### `WorldPawns.AllPawnsAlive` / `AllPawnsAliveOrDead`

保留依据：1.6 原版 getter 仍然会清空并重建结果列表，`AllPawnsAliveOrDead` 还会先拼 `AllPawnsAlive` 再拼 `AllPawnsDead`，因此它仍然值得在 world 相关场景里保留怀疑。

结论：world 场景下保留雷达，不写成近期待办。

### `ImmunityHandler.NeededImmunitiesNow` / `TryAddImmunityRecord`

保留依据：1.6 原版 `NeededImmunitiesNow()` 仍然逐个扫描 hediff，`TryAddImmunityRecord` 仍然通过 `ImmunityRecordExists -> GetImmunityRecord` 线性检查免疫记录是否已存在。

结论：仍可疑，但需要疾病 / 免疫链路上的专题 profiler。

### `GenLocalDate.DayTick(Map)`

保留依据：1.6 原版 `DayTick(Map)` 仍然只是转发到 `DayTick(map.Tile)`，没有 map 级缓存；如果某个系统高频重复按同一张地图取 day tick，这条仍可能值得看。

结论：保留怀疑，但先等更明确的调用现场。

## 在 1.6 下优先级下降，或不建议投入

### `GasGrid`

结论：PF 在这里做的是位运算 + 并行化 + 多网格替换的整套子系统重写，而 1.6 原版 `GasGrid.Tick()` 本身也已经按地图面积固定比例抽样处理扩散和消散，因此不建议投入。

### `WorldObjectsHolderTick`

结论：PF 在这里的收益依赖“哪些 world object 可以少 tick”的行为改写，不是外科式等价补丁，因此不建议继承。

### `WorldPawnGC`

结论：PF 在这里改的是 GC 节奏和保留规则，这不是单纯优化，而是行为敏感逻辑，因此不建议继承。

### `ThingFilter.BestThingRequest` / `ThingsMatching`

结论：PF 这条路线依赖对 `ThingRequest` 表示层做重 hack，改动面和验证成本都过高。

### `ThingWithComps.GetComp<T>`

结论：1.6 原版中已经部分优化，当前不建议单独立项。

### `Room.Role` / `Room.Owners`

结论：PF 这里的主要收益来自“少更新”，不是“更快算出同一个结果”，因此不建议投入。

### `DesignationManagerCaching`

结论：1.6 原版已经维护了 `designationsByDef`、`designationsAtCell`、`thingDesignations` 等索引，当前已经缺少明显立项价值。

### `MeditationUtility.PsyfocusGainPerTick`

结论：1.6 原版实现本身很短，只是取两个 stat 再除以 `60000f`，当前缺少把它当成单独热点的依据，因此不建议优先投入。

### `ReflectionCaching` / `GenTypes` / `ModsConfig` / `ModLister`

结论：这类基础设施 patch 更像大型基础设施 mod 的领域，不是当前最值得投入的 gameplay 热点。

### `Defs/ThingDefPatches.cs` 这类大范围 Def 行为改写

结论：这组 patch 混杂缓存、异常兜底和 Def 语义改写，改动面、行为风险和验证成本都明显偏高，因此不建议投入。
