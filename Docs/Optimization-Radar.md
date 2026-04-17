# Optimization Radar

面向项目：`Kingfisher`  
目标版本：`RimWorld 1.6`

## 实现约定

1. 语义上属于完整方法体替换时，优先用 `Prepatcher`
2. 状态缺失或索引缺失时，考虑 `Prepatcher` 字段注入
3. 只是局部挂钩、前后置修正或状态同步时，保留 `Harmony`
4. 以 aggregate 为正式的 AB 测试

## 已落地

### 当前代码里已存在

### `ListerThings.Remove`

- 方案：`Prepatcher` 方法体替换

### `ListerBuildings`

- 方案：`Prepatcher` 查询改写 + `Harmony` 维护 colonist building 索引

### `AttackTargetFinder.BestAttackTarget`

- 方案：`Prepatcher` 方法体替换

### `PawnDiedOrDownedThoughtsUtility`

- 方案：`Prepatcher` 方法体替换 + `Harmony` 接管 `TryGiveThoughts`

### `ImmunityHandler.NeededImmunitiesNow`

- 方案：`Prepatcher` 方法体替换 + 缓存结果列表

### 蹒跚怪空闲索敌轮询

- 方案：`Harmony` 局部挂钩，把空闲态索敌从轮询改为事件驱动优先

### 已验证并推荐长期保留

### `ImmunityHandler.NeededImmunitiesNow`

- 结论：收益明确，补丁已确认真实生效

### 蹒跚怪空闲索敌轮询

- 结论：`idle` 索敌基本被压掉，总账和专题 profiler 都验证了正收益

## 已证伪

### `HediffComp_TendDuration`

- 状态：证伪
- 结论：局部热点没有转化成 aggregate 总账收益，不保留补丁

### `HediffDef.PossibleToDevelopImmunityNaturally`

- 状态：证伪
- 结论：真正有效的是 `NeededImmunitiesNow` 结果缓存，这个 def 级小判断本身不值得单独优化

### `HediffSet.GetFirstHediffOfDef` / `HasHediff`

- 状态：证伪
- 结论：线性扫描属实，但当前场景里 `avgHediffCount` 太低，绝对成本不成立

### `StoreUtility.NoStorageBlockersIn`

- 状态：证伪
- 结论：调用频繁但单次过轻，不足以支撑 `StorageBlockerGrid` 路线

### `ListerHaulables.ListerHaulablesTick`

- 状态：证伪
- 结论：高层入口相对显眼，但绝对值仍不足以立项

### `Room.ContainedBeds`

- 状态：证伪
- 结论：实现不漂亮，但调用频率和绝对耗时都太低

## 暂缓

### 爆炸链路

- 涉及：`ExplosionCellsToHit`、爆炸上下文中的 `GenSight.LineOfSight`、`Explosion.Tick -> AffectCell -> DamageThing -> TakeDamage -> AddHediff`
- 状态：暂缓
- 结论：做过定位，但还没找到低风险、可稳定复现、值得正式落地的切点

## 专题雷达

这些方向仍值得保留，但应按专题 profiling 和专题设计推进，不写成“小 patch 待办”。

### Work / Haul / Storage

- `JobGiver_Work.TryIssueJobPackage`
- `WorkGiver_Haul.PotentialWorkThingsGlobal`
- `JobGiver_Haul.TryGiveJob`
- `ListerHaulables`
- `StorageSettings.AllowedToAccept`

结论：这里的关键不在某个小缓存，而在先做更便宜的筛选，再减少后续昂贵检查次数。

### DoBill / Ingredient Search / Holder Traversal

- `WorkGiver_DoBill`
- `ThingOwnerUtility.GetAllThingsRecursively`
- `RecipeDef.AllRecipeUsers`

结论：这是一整个专题，应统一看，不拆成零散小 patch。

### World

- `WorldObjectsHolder.WorldObjectsHolderTick`
- `WorldPawns.AllPawnsAlive`
- `WorldPawns.AllPawnsAliveOrDead`

结论：world 子系统仍有可疑点，但需要 world 场景下的专门 profiling。

### 其他仍可疑方向

- `ThingDef.GetCompProperties<T>` / `HasComp*`
- `ThingOwner<T>.Remove`
- `Pawn_GeneTracker.GeneTrackerTick`
- `DynamicDrawManager.DrawDynamicThings`
- `MassUtility.GearMass` / `InventoryMass`
- `GenLocalDate.DayTick(Map)`

结论：都还不该写成近期待办，先等更明确的调用现场和专题 profiler。

## 不建议投入

### 子系统级重写或行为敏感改写

- `GasGrid`
- `WorldObjectsHolderTick`
- `WorldPawnGC`
- `ThingFilter.BestThingRequest` / `ThingsMatching`
- `Defs/ThingDefPatches.cs` 这类大范围 Def 行为改写

结论：改动面和行为风险都过高，不适合当前项目阶段。

### 当前立项价值偏低

- `ThingWithComps.GetComp<T>`
- `Room.Role` / `Room.Owners`
- `DesignationManagerCaching`
- `MeditationUtility.PsyfocusGainPerTick`
- `ReflectionCaching` / `GenTypes` / `ModsConfig` / `ModLister`
