# Reflow

High-performance layout for Unity uGUI (Unity 6). Changes are coalesced into one layout per frame that still lands before the frame renders; layout is a measure/arrange pass that only touches the affected branch; lists are pooled and virtualized.

The package lives in [`Packages/com.yuankunhuang.reflow`](Packages/com.yuankunhuang.reflow) (namespace `Reflow`). Its [README](Packages/com.yuankunhuang.reflow/README.md) and [architecture notes](Packages/com.yuankunhuang.reflow/Documentation~/Architecture.md) are in Chinese.

## Install

```json
"com.yuankunhuang.reflow": "https://github.com/YuankunHuang/Reflow.git?path=/Packages/com.yuankunhuang.reflow#v1.0.0"
```

Requires Unity 6000.0 or newer (uGUI 2.0). The only dependency is `com.unity.ugui`.

## This repository

A Unity 6000.3 project used to develop and test the package.

```
Packages/com.yuankunhuang.reflow/   the package: Runtime, Editor, Tests, Samples~, Documentation~
Assets/DevTests/                    smoke tests that run every sample
Assets/Samples/                     samples imported from Samples~ (menu: Reflow Dev > Sync Samples)
Assets/TextMesh Pro/                TMP essential resources used by the tests
```

Run the tests from the Test Runner, or headless:

```
Unity.exe -batchmode -projectPath <this folder> -runTests -testPlatform EditMode -testResults editmode.xml
Unity.exe -batchmode -projectPath <this folder> -runTests -testPlatform PlayMode -testResults playmode.xml
```

The benchmark against uGUI LayoutGroup + ContentSizeFitter is an explicit PlayMode test (`-testFilter Reflow.Tests.ReflowBenchmarkTests`); results go to `Logs/ReflowBenchmark.md`.

To release: bump `version` in `package.json`, add a CHANGELOG entry, commit, tag `v<version>` and push the tag.

## License

[MIT](LICENSE)
