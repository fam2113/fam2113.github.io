---
name: note-content
description: "이현배 작가 웹사이트의 Note 글을 연도별로 추가·수정하고, 한 페이지 Note 목록 및 연도 앵커 내비게이션을 유지할 때 사용한다. Work 이미지 업로드나 Exhibitions 작업에는 사용하지 않는다."
---

# Note 콘텐츠 관리

이 스킬은 이 저장소의 Note 글을 현재의 한 페이지 목록 구조 안에서 안전하고 일관되게
추가·수정하는 방법을 정한다. 글의 사실관계와 원고는 사용자가 제공하거나 승인한 내용만
사용한다.

## 현재 구조

- `note/note_main.html`은 모든 Note를 최근 연도부터 한 페이지에 보여 주는 진입점이다.
  `note_years` 목록의 순서로 연도별 파일을 불러오며, 각 묶음의 앵커는
  `#note-year-<연도>`다.
- `note/<연도>.html`은 해당 연도의 원고와 마크업을 직접 가진다. front matter의
  `note_year`는 숫자로 유지한다.
- `_includes/note_period_nav.html`의 연도 링크는 모두
  `/note/note_main.html#note-year-<연도>` 형식이다.
- `_includes/header.html`은 `/note/note_main.html`을 가리키며,
  `_layouts/default.html`은 `note_year` 또는 `note_index` 페이지에서 연도 내비게이션을
  표시한다.
- `_includes/note_archive.html`은 사용하지 않는다. 새 공용 archive include를 만들거나
  원고를 그 파일로 옮기지 않는다.

## 기존 글 수정

1. 대상 연도의 `note/<연도>.html`과 앞뒤 연도 파일을 읽어 제목, 작성자, 날짜, 본문,
   링크 마크업을 확인한다.
2. 해당 연도 파일만 필요한 범위에서 수정한다. `note_year`, 문서 `id`, 접근성 속성은
   기존 규칙을 유지한다.
3. HTML 들여쓰기는 공백 2칸으로 맞추고 탭을 쓰지 않는다. 구조나 문단 배치가 달라지는
   경우에는 같은 파일 또는 인접 연도의 마크업을 기준으로 한다.

## 새 연도 추가

1. 가장 가까운 기존 연도 파일을 기준으로 `note/<연도>.html`을 만든다. 페이지 front
   matter에 숫자형 `note_year`를 넣고, 원고는 그 파일 안에 직접 작성한다.
2. `note/note_main.html`의 `note_years`에 연도를 최신순으로 추가한다. 이 목록에 없으면
   파일이 있어도 한 페이지 목록에 표시되지 않는다.
3. `_includes/note_period_nav.html`에 같은 연도 링크를 최신순으로 추가한다. 링크는 반드시
   해당 `note-year-<연도>` 앵커로 연결한다.
4. 새 연도가 현재 최신 Note라면 `_includes/header.html`의 Note 진입 링크도
   `note_main.html`을 계속 가리키는지 확인한다. 특정 연도 링크로 바꾸지 않는다.

## 이미지와 링크

- 새 이미지가 필요한 경우 저장소에 원본 이미지를 추가하지 않는다. 사이트 이미지 정책에
  따라 Azure Storage의 공개 URL을 사용한다.
- 외부 링크는 기존 Note와 같은 방식으로 `target`, `rel`, 접근성 문구를 맞춘다. 링크
  대상이나 인용 출처를 추측해 만들지 않는다.

## 확인과 마무리

1. 시작 전 작업 트리 상태를 확인하고, 안전할 때만 `origin/main`을 fetch한 뒤
   `git pull --ff-only origin main`을 실행한다. `main` 이외의 브랜치를 만들거나 전환하지
   않는다.
2. 수정 뒤 `git diff --check`로 공백 오류를 확인하고, `rg`로 삭제된
   `note_archive.html` 참조가 생기지 않았는지 점검한다.
3. Jekyll build를 실행한다. 목록 또는 내비게이션을 바꿨다면 로컬 사이트에서
   `/note/note_main.html`과 추가·수정 연도의 `#note-year-<연도>` 이동을 직접 확인한다.
4. 요청 범위의 diff만 남았는지 확인한 뒤, 짧은 한 줄 한국어 메시지로 commit·push한다.
   GitHub 인증·권한 문제는 우회하지 말고 결과를 알린다.
