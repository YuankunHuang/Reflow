# Reflow 架构说明

## 1. 目标

1. **改动当帧生效，一帧只排一次版。** 同一帧里无论怎么改、改多少次，都在本帧渲染前排版完毕，而且每个受影响的排版根只排一次。不需要调用方自己去重，也不存在延迟一帧的刷新方式。
2. **只重算受影响的那一支。** 改一个 label，只重排从它向上、到第一个尺寸不依赖内容的容器为止的这一段链路。
3. **稳态零 GC。** 重排和滚动都不分配内存。
4. **在 uGUI 之上工作。** 使用 RectTransform 和 TMP，不改渲染管线，也能和 uGUI 的 LayoutGroup 共存。

## 2. 节点与协议

`ReflowNode`（抽象类，继承 UIBehaviour，一个 GameObject 上只能有一个）有两类实现：
- 容器：`ReflowLayout` 及其子类；
- 叶子：`ReflowFitter`。

```
Measure(fixedSize) -> desiredSize      尺寸从下往上报。fixed[axis] >= 0 表示这个轴的尺寸已经定了；FREE 表示按内容定
Arrange(finalSize)                     父级定下尺寸后调用：先写子节点的 rect，再递归 Arrange 子节点
FollowsContent(axis)                   这个轴的尺寸是否取决于内容
```

- **测量缓存**：`Measure` 按 fixedSize 缓存结果，节点被标脏时失效。
- **排布跳过**：节点没被标脏、而且拿到的尺寸和上次一样时，`Arrange` 直接跳过，整棵子树都不碰。
- **父级怎么传 fixed**：
  - 父级在测量之前就知道子节点尺寸的轴，直接把这个尺寸作为 fixed 传下去。比如交叉轴是 Expand、Fixed 模式、网格的 Stretch。这样子节点的内容（比如换行的文字）按最终宽度测量，一遍就对。
  - 主轴分配下来的尺寸如果和子节点测量出的尺寸不一样，父级会把子节点的主轴尺寸固定为分到的值，再测一次交叉轴。这就是"文字被挤窄后换行变高"的情形。
- **自然尺寸**：普通子节点的自然尺寸从 rect 读，并且和"上次布局写入的值"比较。rect 上还是布局写进去的值，就沿用原来记住的自然尺寸，避免把 Expand 撑大后的尺寸读回来当成自然尺寸。

## 3. 标脏与排版边界

```
SetDirty(node):
    node.dirty = true
    如果 node 的尺寸跟随内容，并且父容器会给它排版  -> SetDirty(父容器)
    否则                                          -> 把 node 放进调度队列（它就是排版边界）
```

- **不跟随内容的容器就是排版边界。** 比如固定大小的面板，或者 ScrollRect 里宽度拉伸、高度跟随内容的 content，后者的父级不是 Reflow 容器。边界内部发生变化，外面不需要重排。
- 父容器正在生成或测量某个子节点时（`_suppressChildDirtyDepth`），子节点的变化不往上传。父容器会在生成结束后统一复测，所以生成一批条目不会引发额外的整体重排。
- 标脏是幂等的：已经脏了就直接返回，所以一帧里调多少次都一样。节点在未激活时被标脏，会在重新激活时补上这次排版。

## 4. 调度（`ReflowScheduler`）

队列在三个时间点清空：

| 刷新点 | 作用 |
|---|---|
| PlayerLoop `PreLateUpdate`，在 `ScriptRunBehaviourLateUpdate` 之前 | Update 里做的改动，LateUpdate 里就能读到结果 |
| PlayerLoop `PostLateUpdate`，在 `UpdateRectTransform` 之前 | LateUpdate（包括 ScrollRect 的滚动回调）里做的改动，本帧照样能画出来 |
| `Canvas.preWillRenderCanvases` | 让 `Canvas.ForceUpdateCanvases()` 成为完整的读屏障；编辑态也靠它刷新；PlayerLoop 插入失败时兜底 |

一次清空的过程：
1. 按层级深度排序，浅的先排。上层排版时会顺带把下层脏节点排掉，排到下层时它已经干净，直接跳过。
2. 排版中如果又产生新的脏节点（比如某个回调又改了别的布局），放到下一遍处理，最多 8 遍。超过 8 遍就报一次错，剩下的留到下一个刷新点处理，不会丢。
3. 每一遍结束时检查各 `constraintSource` 的尺寸有没有变。

**读屏障** `EnsureLayout()`：沿父容器链往上找还在排队的节点，从最外层开始处理。已经在排版中的节点会跳过，不会重入。所有读结果的接口都先走一遍读屏障。

## 5. 容器内部

- **一次 Solve**：
  1. 构建 slot 数组：先是场景子节点，再是托管元素。
  2. 逐个测量。
  3. 按容器的尺寸策略定下容器尺寸。
  4. `ReflowAxisSolver` 分配主轴：Natural / Capped / Expand。按权重分配时，碰到上下限的子节点就冻结，把省下或多占的空间重新分给其余子节点。
  5. 定交叉轴尺寸，计算位置。
- **Measure 做完的 Solve 结果，Arrange 直接复用。** 版本号对得上、尺寸一样就不再算第二遍。
- **组件缓存**：子节点列表和子节点上的组件，只在增删子节点、组件启用或禁用时重新获取。排版过程中不做任何 `GetComponent*` 调用。
- **写 rect**：
  - 值变了才写。
  - 锚点统一改为父级左上角，保留子节点的 pivot。
  - 编辑器下用 `DrivenRectTransformTracker` 把这些字段标成"由布局驱动"，打包后这段代码不存在。

## 6. 虚拟化

- **何时开启**：`Auto` 模式下找到 viewport 就开启。查找顺序：显式指定的字段 → 父级 ScrollRect 的 viewport → 名为 "Viewport" 的祖先节点。找到 ScrollRect 时会订阅它的 `onValueChanged`。
- **可见区间**：slot 在主方向上单调排列，用两次二分查找得到可见区间，复杂度 O(log n + 可见数)，再用副方向的范围过滤一遍。
- **滚动**：只做生成和回收，不重新排版。新生成的条目会测量一次，只有测出来的尺寸和之前的估计不一样时，才会标脏重排（仍然在本帧内完成）。
- **还没生成过的条目用什么尺寸**：上次测得的尺寸 → sizeHint → prefab 尺寸 → 默认尺寸，依次取第一个可用的。
- **测量校正锚点**：新生成的条目测出的尺寸和估计不一样，会导致重排。这时以"上一轮就已经在屏幕上的第一个条目"为锚，重排后把 content 平移回去，让它留在原位。
  - 效果类似 CSS 的 scroll anchoring，但只针对测量校正这一种情况。
  - 典型场景是往上滚动、进入没测量过的行：画面不会跳。
  - 数据变动（插入、删除等）不会自动锚定，需要时显式调用 `CaptureScrollAnchor` / `RestoreScrollAnchor`，或者 `ReorderElements(keepScrollAnchor)`。
  - 使用 `ReflowScrollRect` 时，平移会同步到正在进行的拖拽起点和滚动动画，拖拽不会被拉回去。
- **定位滚动跟踪目标**：`ScrollToElement` / `RevealElement` 在动画过程中每帧按元素的当前位置重算目标，到达之后再跟随几帧。瞬间跳转时，也会在同一次调用里反复对齐到位。这样即使途中生成的行校正了尺寸，也能准确落到目标上。
- **对象池**：按 poolKey 分组，默认用 prefab 本身。
  - 回收的条目要到这一遍结束才真正 `SetActive(false)`。同一遍里又被取出来复用的条目，就不会经历"关掉再打开"。
  - 条目生成前会把尺寸恢复为 prefab 的原始尺寸，避免带着上一个元素的尺寸去测量。

## 7. 为什么比 uGUI 自带的 LayoutGroup 快

| uGUI | Reflow |
|---|---|
| 被动标脏：任何文字、尺寸或激活状态变化，都会沿 ILayoutGroup 链一路标到最外层 | 显式标脏，遇到排版边界就停 |
| `LayoutUtility` 对每个子节点、每个轴，取 min、preferred、flexible 时各做一次 `GetComponents(ILayoutElement)` | 组件引用缓存在 slot 里，排版时不做 GetComponent |
| `LayoutRebuilder` 分四遍遍历：横向计算、横向设置、纵向计算、纵向设置 | 测量一遍、排布一遍，Solve 结果复用，没变化的子树整个跳过 |
| 从布局根开始整棵重建 | 只重算受影响的那一支，兄弟节点的测量结果直接命中缓存 |
| 所有子节点都是真实存在的 GameObject | 虚拟化加对象池，1000 行的列表只存在十几个条目 |

和 museumclient 旧版 IGGLayout 相比，去掉了这些浪费：
- 每次刷新跑 2 轮、每轮上下各一遍，导致每个子布局被刷 4 次；
- 每次刷新都调用 `GetComponentsInChildren`；
- fitter 调用 `Canvas.ForceUpdateCanvases()`；
- 条目生成后先写入自然尺寸，再触发一次整体重排；
- 滚动时对所有元素做 O(n) 的可见性扫描；
- 同一帧内重复刷新，靠调用方自己去重。

## 8. 基准

测试工程：`ReflowBenchmarkTests`（PlayMode，Explicit），或者 `Samples~/Benchmark`。两边搭同样的 UI，uGUI 一侧用 LayoutGroup + ContentSizeFitter，只计排版耗时（`LayoutRebuilder.ForceRebuildLayoutImmediate` 对比 `EnsureLayout`），不计图形重建。

测试环境：Unity 6000.3.16f1，Editor（Mono），i7-11700K：

| 场景 | Reflow ms | uGUI ms | 倍数 |
|---|---:|---:|---:|
| 200 行全部改高度后重排 | 0.215 | 1.071 | 5.0x |
| 50 行"图标 + 文字"列表，改其中一个 label | 0.124 | 2.372 | 19.1x |
| 3 层嵌套（10 组 × 10 行），改其中一个 label | 0.087 | 4.876 | 56.3x |
| 构建 1000 行列表（Reflow 只生成 12 个条目） | 1.268 | 22.609 | 17.8x |
| 1000 行列表滚动一步（生成加回收） | 0.217 | – | – |

- 滚动 200 步共分配 0 B。
- 嵌套越深、列表越长，差距越大，因为 Reflow 的开销只和"变化的那一支"有关。
- 真机上（IL2CPP）绝对值会更小，但两边的比例接近。

## 9. 边界情况与约定

- **普通子节点**（不是 Reflow 节点）手动改了尺寸或 SetActive，需要调用 `RefreshAllLayout()`。原因是 uGUI 不会通知父级这类变化。Reflow 节点、`ReflowElement`、TMP 文字的变化都会自动感知。
- 在布局回调里（`Show`、`ElementSpawned`）不允许增删或重排元素，会抛异常，并被记录到日志里。
- Reflow 节点放在 uGUI LayoutGroup 下面时，uGUI 改它的尺寸要到下一帧才会重排，因为 uGUI 自己的重建发生在我们的刷新点之后。反过来，把 uGUI 元素放进 Reflow 容器是当帧生效的。
- `ReflowExpandableGrid` 只支持行优先（StartAxis.Horizontal）。
- 可见区间的二分查找依赖 slot 在主方向上单调排列。内置的所有布局都满足这一点，自定义布局也需要保证。
