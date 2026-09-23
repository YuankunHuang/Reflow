# Changelog

## [1.0.0] - 2026-09-22

首个独立版本，从 museumclient 的 IGGLayout 重写而来。

### Added
- MIT 许可证（包内 `LICENSE.md`）。
- **测量 / 排布协议**：`ReflowNode`，带排版边界、测量缓存、排布跳过。
- **调度**：`ReflowScheduler`，帧内合并刷新。刷新点在 LateUpdate 之前、画布更新之前，以及 `Canvas.preWillRenderCanvases`；另有读屏障 `EnsureLayout`。
- **布局**：
  - `ReflowVertical` / `ReflowHorizontal`：Natural / Expand / Fixed、ScaleToFit、Reverse。
  - `ReflowGrid`：四个起始角、行优先或列优先、Flexible / FixedColumnCount / FixedRowCount、CellFit。
  - `ReflowExpandableGrid`。
- **容器尺寸策略** `ReflowSizePolicy`：follow、min、max、constraintSource。跟随到上限后，flexible 子节点分配剩余空间，其余按比例压缩。
- **托管元素**：对象池、`Auto` 虚拟化、二分查找可见区间、`SetElementHidden`、`ReorderElements`、结构变化事件 `ElementsChanged`（附带 `MapIndex`）。
- **`ReflowFitter`**：Preferred / Rendered 两种模式；min / max；padding 用 TMP margin 实现；可以跟随另一个 fitter（source）；改字的检测走 TMP 的 layout-dirty 回调。
- **`ReflowSelection`**、**`ReflowSpawnAnimator`**、**`RefreshWithAnimation(duration)`**、**`ReflowScrollRect`**（ScrollToElement / RevealElement）。
- **滚动锚点**：`CaptureScrollAnchor` / `RestoreScrollAnchor`；测量校正时自动锚定；`ScrollToElement` 一路跟踪目标元素。
- **编辑器**：各组件的 Inspector、尺寸策略绘制器、Gizmo、`GameObject/UI/Reflow` 创建菜单。
- **测试**：包内 EditMode 94 个，PlayMode 13 个，外加一个 Benchmark；开发工程里另有 5 个示例冒烟测试。

### Fixed（相对旧版 IGGLayout）
- 插入、删除元素后，出场历史和选中索引没有跟着平移。
- ExpandableGrid 混用公开索引和内部索引，局部刷新的起点也不对。
- `RefreshWithAnimation` 调用两次会叠加两个协程；新元素会从 (0,0) 飞入。
- 缩放动画被单独取消后，缩放没有复原。
- 条目不可选时，选中逻辑仍然会修改当前选中项。
- Center 对齐时没有扣除 padding。
- 非布局子节点在拉伸锚点下定位不准。
- 条目生成后先写入自然尺寸、再整体重排一次，造成多余的一遍排版。

### Removed
- `IGGCircleLayout`（旧库中的环形布局）。
- `RefreshAllLayout(bool)` 的"延迟到下一帧"模式。
- `refreshAnimationSpeed`，改为用时长控制。
