using System.Runtime.CompilerServices;

// 배치 활동(FormationActivity)처럼 생성자를 internal로 막아 둔 타입을 EditMode 테스트가 GameObject 없이 직접 만들 수 있게 한다 -
// MonoBehaviour 저장소를 거치면 Unity 밖 실행(순수 로직 검증)이 불가능해진다(설계 79번 §11).
[assembly: InternalsVisibleTo("Game.Core.Tests")]
