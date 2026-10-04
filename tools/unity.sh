#!/usr/bin/env bash
set -euo pipefail

task_project="$(cd "$(dirname "$0")/.." && pwd)"
task_version="$(sed -n 's/^m_EditorVersion: //p' "$task_project/ProjectSettings/ProjectVersion.txt")"
task_editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/$task_version/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$task_editor" ]]; then
    printf 'Set UNITY_EDITOR to the Unity %s executable.\n' "$task_version" >&2
    exit 1
fi
mkdir -p "$task_project/Logs"
task_args=(-batchmode -projectPath "$task_project")
case "${1:-}" in
    tests)
        task_args+=(-runTests -testPlatform EditMode -testResults "$task_project/Logs/EditMode.xml") ;;
    gameplay)
        task_args+=(-executeMethod HaoxiKaiyan.Editor.HaoxiVerification.RunGameplayBatch) ;;
    motion)
        task_args+=(-executeMethod HaoxiKaiyan.Editor.HaoxiVerification.RunMotionBatch) ;;
    build)
        task_args+=(-quit -executeMethod HaoxiKaiyan.Editor.HaoxiSceneBuilder.BuildWebGlBatch) ;;
    clean-build)
        task_args+=(-quit -executeMethod HaoxiKaiyan.Editor.HaoxiSceneBuilder.BuildWebGlCleanBatch) ;;
    *)
        printf 'Usage: bash tools/unity.sh {tests|gameplay|motion|build|clean-build}\n' >&2
        exit 2 ;;
esac
task_args+=(-logFile "$task_project/Logs/Unity-$1.log")
cd "$task_project"
if [[ "$(uname -s)" == Darwin ]]; then
    # The bundled Burst/Mono tooling needs UTF-8 when MenuItem labels contain Chinese.
    exec env LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 "$task_editor" "${task_args[@]}"
else
    exec "$task_editor" "${task_args[@]}"
fi
