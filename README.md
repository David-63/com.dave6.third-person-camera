# com.dave6.character-kit
**Unity** 에서 캐릭터 컨트롤러를 구축하기 위한 내부용 프레임워크 패키지


## Requirements

- Unity Input System
- Cinemachine 3.1+
- Timer Package
- Unity Util Package
- State Machine Package
- Stat System package
- Game State Flow package
- Object Pooling System
- Surface Reaction System


## Scope (What this package currently handles)
- 캐릭터 행위 흐름의 공통 기반
-   이동 / 공중 / 액션 / 상체 동작 분리 구조
- 상태 기반 캐릭터 로직 (FSM)
- 애니메이션 레이어 및 Rig 기반 동작 제어
-   Aim Rig
-   Hand IK
- 장착 아이템과 캐릭터 간의 런타임 바인딩
-   ActiveItem 개념 포함
- 스탯 시스템과 연동된 캐릭터 능력치 처리
