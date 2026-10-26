# Unity Client Samples

게임 클라이언트 실무에서 반복해서 만난 문제를 **회사 코드와 무관하게 새로 작성한** 샘플 모음입니다.
콘텐츠 시스템 · 인게임 전투 · UI · 네트워크 흐름을 다루고, 샘플마다 "어떤 문제를 왜 이 구조로 풀었는지"를 함께 적었습니다.

- Unity 2022.3 LTS 이상 · C#
- 게임 로직은 MonoBehaviour와 분리한 순수 C# 클래스로 작성해 **EditMode 테스트로 검증**합니다 (`Assets/Tests/Editor`, Test Runner → EditMode).

## 구성

| 분류 | 샘플 | 다루는 내용 |
| --- | --- | --- |
| **인게임** | [AutoBattle](Assets/Samples/InGame/Battle/AutoBattle.cs) | 웨이브 기반 자동 전투(방치형), 유닛 AI 상태 머신, 고정 시간 간격 시뮬레이션, 타깃 선정 |
| | [StateMachine](Assets/Samples/InGame/FSM/StateMachine.cs) | Enter / Tick / Exit 상태 머신, 상태가 다음 상태를 반환하는 전이 방식, 외부 이벤트 강제 전이 |
| **콘텐츠** | [EnhanceSystem](Assets/Samples/Content/Enhance/EnhanceSystem.cs) | 테이블 기반 강화, 요청 전 검증, 서버 결과 적용, 연출 중 유저 선택 보존, 파괴 방지 |
| | [QuestTracker](Assets/Samples/Content/Quest/QuestTracker.cs) | 이벤트 기반 퀘스트 진행, 누적형 / 달성형 구분, 보상 중복 수령 방지, 알림창 우선순위 |
| | [WeightedTable](Assets/Samples/Content/Gacha/WeightedTable.cs) | 가중치 추첨(누적합 + 이분 탐색), 확률 정보 표시와 합계 100% 보정 |
| | [ResultPager](Assets/Samples/Flow/ResultPager.cs) | 연속 소환 · 합성 결과의 N장 단위 진행, 자동 진행, 상태의 주인 하나 |
| **데이터** | [TableRepository](Assets/Samples/Data/TableRepository.cs) | 기획 테이블 ID 조회, 로드 시 중복 ID 검출 |
| **네트워크** | [SequentialRequestQueue](Assets/Samples/Network/SequentialRequestQueue.cs) | 연속 요청 순차 처리, 응답 역전 · 동시 요청 오류 방지, 실패 시 잔여 요청 취소 |
| **입력** | [RequestLock](Assets/Samples/InputControl/RequestLock.cs) | 요청 ~ 연출 종료까지 입력 잠금, 실패 · 타임아웃 시 해제 |
| **UI** | [RecycleScrollView](Assets/Samples/UI/RecycleScrollView.cs) | 보이는 셀만 생성하는 재사용 스크롤, Layout Group 없는 위치 계산 |
| | [PopupStack](Assets/Samples/UI/PopupStack.cs) | 팝업 공용 스택, ESC · 뒤로가기 규칙, 딤드 1개 |
| | [RedDotTree](Assets/Samples/RedDot/RedDotTree.cs) | 경로 기반 레드닷, 조건 단일 계산, 변경 경로만 전파 |

---

## 인게임

### AutoBattle — 프레임과 무관하게 같은 결과

```
Update(deltaTime) ──▶ accumulator ──▶ Simulate(0.1s) × N
                                         │
                     각 유닛 StateMachine.Tick
                     Idle ──적 발견──▶ Move ──사거리 안──▶ Attack ──적 사망──▶ Idle
                                                        │
                                             ApplyDamage ─▶ Damaged / Died 이벤트 ─▶ View 연출
                     적 전멸 ─▶ WaveCleared ─▶ 다음 웨이브 생성
```

- 전투 로직은 `BattleUnit`(데이터) + `AutoBattle`(규칙)로 MonoBehaviour와 분리했습니다. View는 이벤트만 받아 애니메이션 · 데미지 숫자를 띄웁니다.
- `Update`에 들어온 deltaTime을 0.1초 고정 간격으로 나눠 진행합니다. 프레임 레이트가 달라도 결과가 같아서 테스트 · 리플레이 · 서버 검증이 가능합니다 (`SameInput_SameResult` 테스트).
- 타깃은 "가장 가까운 적, 동률이면 낮은 ID"로 고정해 결과가 흔들리지 않게 했습니다.

### StateMachine

- 상태는 `Tick`에서 다음 상태 키를 돌려주는 방식으로만 전이합니다. 전이 조건이 상태 안에 모여 있어 "어디서 상태가 바뀌었는지"를 찾기 쉽습니다.
- 피격 · 사망 같은 외부 이벤트는 `ChangeTo`로 강제 전이합니다.
- 캐릭터 AI뿐 아니라 콘텐츠 진행(대기 → 연출 → 결과)에도 같은 틀을 씁니다.

## 콘텐츠

### EnhanceSystem — 클라이언트는 검증, 결과는 서버

```
Select(step) ─▶ CanRequest (진행 중? 최대 단계? 재료?) ─▶ Request ─▶ [서버]
                                                                    │
                     OnResponse: 진행 중 해제 → CurrentStep = 서버 값
                                → 성공 && 유저가 선택을 안 바꿨으면 다음 단계 자동 선택
                                → ResultArrived(연출) → Changed(화면 갱신)
```

- 버튼 활성화 조건은 `CanRequest` 한 곳에서만 계산하고, 화면은 그 결과만 봅니다.
- 실패 응답에서도 `IsRequesting`을 반드시 해제합니다.
- 연출을 기다리는 동안 유저가 다른 단계를 골랐다면 응답 후 자동 이동하지 않습니다. 유저 선택이 덮어써지는 버그를 막습니다.
- 확률은 만분율 정수로 다뤄 부동소수 오차 없이 표시합니다.

### QuestTracker — 콘텐츠는 퀘스트를 몰라도 된다

```csharp
// 전투 코드는 이벤트만 보낸다
questTracker.Report(QuestKind.KillMonster, monsterId, 1);
// 레벨업 코드도 마찬가지
questTracker.Report(QuestKind.ReachLevel, 0, newLevel);
```

- 종류별 인덱스로 해당 종류 퀘스트만 갱신합니다.
- 누적형(처치 · 수집)은 더하고, 달성형(레벨 · 건물)은 최댓값으로 판단하며 "0 / 1, 1 / 1"로 표시합니다.
- `Completed`와 `Rewarded`를 분리해 보상을 한 번만 받게 합니다.
- 알림창은 메인 퀘스트 → 완료된 퀘스트 → 진행률 높은 순으로 고릅니다.

### WeightedTable — 확률 표시까지

- 누적 가중치 + 이분 탐색으로 항목이 많아도 빠르게 뽑습니다. `PickByRoll`에 값을 직접 넣어 테스트에서 결과를 고정합니다.
- 확률 정보 화면의 표시 값 합계가 100.00%가 되도록 반올림 오차를 보정합니다.

### ResultPager

- 연속 소환 · 합성 결과를 N장 단위로 넘길 때 회차 · 자동 진행 · 마지막 여부를 이 객체만 바꿉니다. 화면은 `Changed`를 받아 "n / N"과 버튼을 다시 그립니다.

## 네트워크 · 입력

- **SequentialRequestQueue**: 일괄 등록 · 구매처럼 연속 요청이 나가는 기능에서 앞 응답을 받은 뒤 다음 요청을 보냅니다. 실패하면 남은 요청을 보내지 않습니다.
- **RequestLock**: 요청부터 결과 연출 종료까지 입력을 잠그고, 실패 · 타임아웃에서도 반드시 해제합니다.

## UI

- **RecycleScrollView**: 보이는 개수 + 여유분만 셀을 만들고 스크롤에 따라 재사용합니다. Layout Group 없이 위치를 직접 계산하고, 반납할 때 `Clear()`로 상태를 지웁니다.
- **PopupStack**: ESC · 뒤로가기는 최상단 팝업만 닫고, 딤드는 하나만 최상단 바로 아래에 둡니다. 같은 팝업 중복 열기를 막습니다.
- **RedDotTree**: `Menu/Research/Slot1` 같은 경로로 레드닷을 관리합니다. 조건은 데이터에서 한 번만 `Set`하고, 변경된 경로의 조상만 다시 계산합니다.

---

## 테스트

| 파일 | 검증 내용 |
| --- | --- |
| `ContentAndBattleTests.cs` | 강화 성공 시 다음 단계 선택 · 유저 선택 보존 · 중복 요청 차단 · 실패 시 해제 · 재료 부족 / 퀘스트 완료 · 중복 수령 차단 · 달성형 표시 · 알림 우선순위 / 가중치 구간 · 확률 합 100% / 웨이브 클리어 · 프레임 간격과 무관한 동일 결과 |
| `SampleLogicTests.cs` | 레드닷 전파 · 형제 유지 · 중복 알림 없음 / 결과 페이지 계산 · 종료 / 요청 큐 순차 전송 · 실패 시 취소 |

## 작성자

이용희 · Unity 클라이언트 개발자
