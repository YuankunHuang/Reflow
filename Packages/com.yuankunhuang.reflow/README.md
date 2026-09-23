# Reflow (`com.yuankunhuang.reflow`)

用于 uGUI 的高性能布局包，Unity 6（uGUI 2.0，TMP 已并入 ugui）可用。

- **当帧生效的合并刷新**：任何改动都只是标脏。同一帧内改多少次，都只排一次版，而且在这一帧渲染之前完成。
- **测量 / 排布两阶段**：尺寸从下往上报，位置从上往下定。只重排受影响的那一支，一遍收敛。
- **数据驱动的托管元素**：对象池复用；放在 viewport 里时自动虚拟化，只生成看得见的条目；可见区间用二分查找。
- 纵向、横向、网格、可展开网格布局，TMP 尺寸适配，选中态，出场动画和重排动画，以及滚动定位。
- 稳态下排版和滚动都**零 GC**。

更深入的设计说明、性能原理和基准数据见 [Documentation~/Architecture.md](Documentation~/Architecture.md)。

## 安装

两种方式任选一种：

- 把 `com.yuankunhuang.reflow` 目录放进工程的 `Packages/` 下，作为内嵌包使用。
- 在 `Packages/manifest.json` 里用 git 引用：
  ```json
  "com.yuankunhuang.reflow": "https://github.com/YuankunHuang/Reflow.git?path=/Packages/com.yuankunhuang.reflow#v1.0.0"
  ```
  `#v1.0.0` 是发布标签，去掉就跟随 main。仓库目前是私有的，安装的机器需要有访问权限（git 凭据能访问 GitHub 即可）。

唯一的依赖是 `com.unity.ugui` 2.0.0。代码里 `using Reflow;`，编辑器扩展在 `Reflow.Editor`。组件在 Add Component 菜单的 **Layout/Reflow** 下，现成的滚动列表在 **GameObject/UI/Reflow** 下。

## 快速开始

### 静态排版（prefab 里摆好的子节点）

1. 在容器上挂 `ReflowVertical`、`ReflowHorizontal` 或 `ReflowGrid`。
2. 需要容器随内容变大变小时，打开 Width / Height 的 **Follow Content**。
3. 文字节点挂 `ReflowFitter`：Horizontal Fit / Vertical Fit 设为 Preferred。改字后布局自动当帧更新，不需要调用任何接口。

编辑态下同样实时排版。

### 数据驱动的列表

```csharp
// content 上挂 ReflowVertical，打开 FollowHeight；放在 ScrollRect 的 Viewport 下就会自动虚拟化。
ReflowVertical list = content.GetComponent<ReflowVertical>();
list.Clear();
foreach (MailData mail in mails)
    list.AddElement(mailItemPrefab, mail);           // 标脏即可，本帧渲染前统一排版

// 需要马上读结果时，查询接口会先把挂起的排版做完（读屏障）
MailItem first = (MailItem)list.GetActiveElement(0);
scrollRect.ScrollToElement(list, unreadIndex, ReflowScrollAlignment.Center);
```

条目 prefab 实现 `IReflowItem`，或者继承便捷基类：

```csharp
public class MailItem : ReflowItemBehaviour<MailData>
{
    [SerializeField] private TMP_Text _title;
    protected override void OnShow(MailData pData) => _title.text = pData.title;
}
```

要接入自己的 UI 框架（比如子 UI 控制器），实现 `IReflowItemFactory`，然后调用 `list.InitOwner(factory)`。

## 组件一览

| 组件 | 作用 |
|---|---|
| `ReflowVertical` / `ReflowHorizontal` | 线性排布。`MainAxisMode` / `CrossAxisMode` 各有三种：Natural / Expand / Fixed。另有 `ScaleToFit`、`ReverseArrangement`、`ChildAlignment` |
| `ReflowGrid` | 等大格子。Flexible / FixedColumnCount / FixedRowCount；四个起始角，行优先或列优先；`CenterSingleRow`；`CellFit` 可选 Stretch / Natural / ScaleToFit |
| `ReflowExpandableGrid` | 在某个元素所在行的下方插入一块整行宽的展开内容，下面的行整体下移 |
| `ReflowElement` | 单个子节点的设置：忽略、手动尺寸、允许布局改宽或改高、flexible 权重、min / max |
| `ReflowFitter` | 按 TMP 文本（Preferred 或 Rendered）或另一个 fitter 定尺寸；padding、min / max，超过 max 换行 |
| `ReflowSelection` | 单选，比如页签。插入、删除、重排后选中项跟着走；`ClickFilter` 可以拦截点击 |
| `ReflowSpawnAnimator` | 首次出现的条目依次播放 Fade / FadeSlide / Scale，顺序可选 Sequential 或 Diagonal |
| `ReflowScrollRect` | ScrollRect 子类：设置 normalizedPosition 前先完成排版；`ScrollToElement` / `RevealElement` 支持动画 |

### 容器尺寸策略（`WidthPolicy` / `HeightPolicy`）

- `follow`：容器尺寸 = 内容尺寸（含 padding）。
- `min` / `max`：跟随时的上下限，0 表示不限。min 优先于上限。
- `constraintSource`：动态上限，取另一个 rect 的当前尺寸（比如父级槽位或 viewport），和 max 取更紧的那个。

跟随到上限、内容放不下时，flexible 子节点分剩下的空间；还放不下，就把布局能控制的子节点按比例压缩，并遵守各自的 min。被 min 撑大时，内容按 `ChildAlignment` 摆放。

## 刷新模型

| 场景 | 需要做什么 |
|---|---|
| `AddElement` / `InsertElement` / `RemoveElement` / `ReorderElements` / `UpdateElementData` / `Clear` | 什么都不用做，调用时已自动标脏 |
| 改 TMP 文字、字号、字体 | 什么都不用做，`ReflowFitter` 会收到 TMP 的回调 |
| 改 `ReflowElement` 的属性，改布局自身的属性 | 什么都不用做 |
| 子节点被启用 / 禁用，而它自己是 Reflow 布局或 fitter | 什么都不用做 |
| 手动改了普通子节点的尺寸，或者 `SetActive` 了一个普通子节点 | 调用 `RefreshAllLayout()`。只是标脏，调多少次都行 |
| 标脏后要马上读结果 | 调 `EnsureLayout()`，或者直接用查询接口 |

排版发生在三个时间点：

1. LateUpdate 脚本执行之前，所以在 Update 里改的东西，LateUpdate 里就能读到结果。
2. RectTransform 和画布更新之前，所以在 LateUpdate 里改的东西，本帧照样能画出来。
3. `Canvas.preWillRenderCanvases`，所以 `Canvas.ForceUpdateCanvases()` 也是一个完整的读屏障。编辑态也靠这个刷新。

**读屏障**：`EnsureLayout`、`GetActiveElement`、`GetElementRect`、`GetElementScrollOffset`、`CaptureScrollAnchor` / `RestoreScrollAnchor`，以及 `ReflowScrollRect` 的 normalizedPosition 和各种定位接口，都会先把挂起的排版做完再返回。

## 变高列表

- `AddElement` 时给一个接近实际的 `sizeHint`，初次估计就更准。
- 条目第一次出现时会测出真实尺寸。如果和估计不同，布局会自动锚定当时已经在屏幕上的条目，所以往上滚动进入没测量过的行时，画面不会跳。
- `ScrollToElement` 会一路跟踪目标元素，即使途中的行被校正了尺寸，也能准确停在目标上。
- 插入、删除元素这类数据变动**不会**自动锚定。需要保持视口位置时，调用 `CaptureScrollAnchor()` 和 `RestoreScrollAnchor()`。

## 从 museumclient 的 IGGLayout 迁移

| 旧 | 新 |
|---|---|
| 命名空间 `com.igg.ui` | `Reflow` |
| `IGGVerticalLayout` / `IGGHorizontalLayout` / `IGGGridLayout` / `IGGExpandableGridLayout` | `ReflowVertical` / `ReflowHorizontal` / `ReflowGrid` / `ReflowExpandableGrid` |
| `IGGLayoutBase` | `ReflowLayout`（容器基类；所有节点的基类是 `ReflowNode`） |
| `IGGLayoutElement` / `IGGSizeFitter` | `ReflowElement` / `ReflowFitter` |
| `IGGLayoutItem` / `IGGLayoutItemFactory` | `IReflowItem` / `IReflowItemFactory` |
| `RefreshAllLayout(bool)`、`RefreshLayoutBottomUp`、`RefreshLayoutFrom` | `RefreshAllLayout()`（只有一个版本，当帧生效）；要马上读结果时用 `EnsureLayout()` |
| `AddElement(IUIInfo, RenderDataBase, prefab, size)` | `AddElement(prefab, data, sizeHint, poolKey)`，池按 `poolKey` 分组，默认用 prefab 本身 |
| `containerFollowChildWidth/Height`、`minWidth`…、`widthConstraintSource` | `WidthPolicy` / `HeightPolicy`（字段 follow / min / max / constraintSource） |
| `childForceExpandWidth/Height` | `CrossAxisMode` / `MainAxisMode` 设为 `Expand` |
| `forceElementSizeX/Y`、`scaleElementSize` | `Fixed` 模式加 `ScaleToFit` |
| `HideElement` / `ShowElement` | `SetElementHidden(index, bool)`，隐藏后不占位 |
| `GetElementVisualY` / `SetElementVisualY` | `CaptureScrollAnchor` / `RestoreScrollAnchor`，横向和纵向都支持 |
| `GetElementScrollPosition(index, vertical, alignment)` | `GetElementScrollOffset(index, alignment)`，或者 `ReflowScrollRect.ScrollToElement` |
| `IGGSelectable*Layout` 四个包装类 | 任意布局加上 `ReflowSelection`，条目实现 `IReflowSelectable` |
| 各种 `*SpawnAnimator` | `ReflowSpawnAnimator`（Effect 加 Order） |
| `RefreshWithAnimation(speed)` | `RefreshWithAnimation(duration)` |
| `IGGSizeFitter.FitMode.MinSize` | `ReflowFitter.FitMode.Rendered`，另外新增 `MinSize` / `MaxSize` |
| `IGGCircleLayout` | 不再提供 |

**行为上的差异**：
- 托管元素的变动会自动标脏。
- 虚拟化默认是 `Auto`：找到 viewport 才开启。
- 子节点的锚点统一改为父级左上角，但保留子节点自己的 pivot（和 uGUI 的 LayoutGroup 一致）。
- 插入、删除时，出场历史和选中索引都会跟着平移。

## 目录

```
Runtime/   Scheduling（调度）、Core（节点协议、求解器）、Layouts、Elements、Items（池）、
           Selection、Animation、Scroll
Editor/    Inspector、尺寸策略绘制器、Gizmo、GameObject/UI/Reflow 创建菜单
Tests/     EditMode（求解器、各布局、Fitter、调度、虚拟化、选中、可展开网格）
           PlayMode（时序、动画、零 GC、Benchmark）
Samples~/  Basics、VirtualizedList、ExpandableGrid、Selection、Benchmark
```

## 许可证

[MIT](LICENSE.md)。
