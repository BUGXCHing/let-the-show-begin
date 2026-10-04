# 参数指南

关键公共默认值集中在 `Assets/Scripts/Core/GameplayTuning.cs`，每种武器的参数在 `Combat/WeaponSpec.cs`。这些都是编译期常量／数据，修改后需让 Unity 重新编译；已有网页要重新构建才会变化。

## 建议阅读与调节顺序

| 目标 | 参数 / 模块 | 当前默认值与含义 |
| --- | --- | --- |
| 最大走速 | `Movement.PlayerSpeed` | 4.35 m/s，后乘移动增益与武器惩罚。 |
| 起步、刹车、换向 | `Movement.PlayerAcceleration` | 29 m/s²；三种情况共用。 |
| 敌人加速 | `Movement.EnemyAcceleration` | 23 m/s²，目标速度由 AI 给出。 |
| 活动范围 | `Movement.ArenaHalfExtent` | XZ 各 ±9.1 m；改变时也要同步 StageBuilder 的地板与边界尺寸。 |
| 角色间距 | `OpposingSpacing / SameTeamSpacing` | 0.69 / 1.05 m；双方动作不应挤进对方关节。 |
| 默认握柄高度 | `Weapon.GripHeight` | 1.38 m；之后仍受手臂可达范围约束。 |
| 右杆触发门槛 | `Weapon.IntentThreshold` | 0.20；不是高度选择或连续力度。 |
| 松杆惯性命中 | `Weapon.InertiaDamageWindow` | 0.52 秒，之后不能继续靠回摆伤人。 |
| 主动轨道响应 | `ActiveOrbitStiffness / OrbitDriveReference` | 155 / 185；乘当前武器 angularDrive 比例和力量增益。 |
| 轨道阻尼 | `LightOrbitDamping / HeavyOrbitDamping` | 12 / 18；按武器质量插值。 |
| 轨道转速上限 | `MaxOrbitSpeed / OrbitSpeedRatio` | 880°/s，且不超过武器刚体转速换算值的 0.72 倍。 |
| 牵引／力矩上限 | `MaxTetherAcceleration / MaxTorqueAcceleration` | 170 / 205，再乘力量增益。 |
| 无效挥动 | `Combat.MinimumSwingSpeed` | 0.65 m/s，静止贴刃不伤人。 |
| 基础重击／击飞档位 | `HeavyForceThreshold / LaunchForceThreshold` | 3.1 / 6.4，是速度与接触比例组成的分档量。 |
| 防投影尖峰 | `Combat.ImpactSpeedCap` | 9.5 m/s，保护伤害计算。 |
| 受击冷却 | `Combat.HitCooldown` | 0.25 秒；配合方向反转／分离条件。 |
| 敌人部位击飞门槛 | `EnemyHeadLaunchSpeed / EnemyKneeLaunchSpeed / EnemyBodyLaunchSpeed` | 6.5 / 9 / 9.3 m/s。 |
| 生命值 | `Combat.PlayerHealth / EnemyHealth` | 180 / 200。 |
| 敌人追击／冲击速度 | `Enemy.ChaseSpeed / StrikeSpeed` | 3.55 / 4.55 m/s。 |
| 胸胯弹簧 | `Pose.Chest* / Pelvis*` | 不同的刚度、阻尼和转速造成牵引先后。 |
| 跑步进入／退出 | `Pose.RunEnterSpeed / RunExitSpeed` | 2.8 / 2.2 m/s，保持滞回稳定。 |

## 武器参数

| 字段 | 作用 |
| --- | --- |
| `mass` | Rigidbody 质量和接触／伤害的约化质量。 |
| `length` | 刀刃范围、扫掠弧长、视觉长度和惯性速度测量。 |
| `drive / angularDrive` | 握柄位置驱动与武器旋转驱动。 |
| `maxAngularSpeed` | Rigidbody 转速上限，单位 rad/s。 |
| `damping` | 刚体线／角阻尼，并参与力矩阻尼。 |
| `sharpness / breakPower` | 锋刃／钝击累计关节损伤。 |
| `movePenalty` | 玩家走速及加速度惩罚。 |

木刀、短刀、长剑、斧、扳手、球棒和重锤已经实现；没有柔性流星锤。外形由基础几何体生成。

| 武器 | mass (kg) | length (m) | drive / angularDrive | maxAngularSpeed (rad/s) |
| --- | --- | --- | --- | --- |
| 木刀 | 0.42 | 1.49 | 245 / 230 | 22 |
| 短刀 | 0.28 | 0.91 | 235 / 210 | 23 |
| 长剑 | 0.66 | 1.68 | 195 / 175 | 19 |
| 斧 | 1.12 | 1.32 | 165 / 145 | 18 |
| 扳手 | 0.78 | 1.08 | 185 / 170 | 20 |
| 球棒 | 0.82 | 1.42 | 180 / 165 | 20 |
| 重锤 | 1.72 | 1.56 | 160 / 150 | 19 |

## 哪些数值仍然靠近代码

IK 骨段长度、脚步预测与落脚周期、肌肉前馈、视觉偏移限制、扫掠采样密度、伤害公式系数和 AI 距离条件仍在对应实现文件中。把所有几何尺寸和公式常量搬进一个大配置会降低可读性，因此只集中公共手感参数；需要改这些算法时读相应专题文档。

## 调节注意

增加武器驱动会改善跟手，也会提高实际刃速、关节损伤与格挡频率。增加走速会缩短腿的支撑时间；改速度后同时观察脚步，不要单独放大数值。

角度弹簧是显式积分，与 fixedDeltaTime 和阻尼有关。大幅提高刚度可能产生振荡；先小幅调整，再运行玩法和动作自检。一次只改一组参数，并记录原值、目的和试玩结论。
