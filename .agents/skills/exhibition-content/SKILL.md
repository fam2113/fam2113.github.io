---
name: exhibition-content
description: "이현배 작가 웹사이트의 Exhibitions 전시 기록과 갤러리를 연도별로 추가·수정하고 전시 내비게이션을 유지할 때 사용한다. Work 작품 업로드나 Note 작업에는 사용하지 않는다."
---

# Exhibitions 콘텐츠 관리

이 스킬은 이 저장소의 Exhibitions 전시 기록을 연도별 파일 안에서 안전하고 일관되게
추가·수정하는 방법을 정한다. 전시명, 기간, 장소, 사진 설명은 사용자가 제공하거나
승인한 정보만 사용한다.

## 현재 구조

- `exhibitions/<연도>.html`은 해당 연도의 전시 정보와 갤러리 마크업을 직접 가진다.
  front matter의 `exhibition_year`는 숫자로 유지한다.
- `_includes/exhibition_period_nav.html`은 연도별 전시 페이지로 가는 내비게이션이다.
  `_layouts/default.html`은 `exhibition_year` 페이지에서 이 내비게이션을 표시한다.
- `_includes/header.html`의 Exhibitions 링크는 최신 전시 페이지로 가는 진입점이다.
- `_includes/exhibitions_archive.html`은 사용하지 않는다. 새 공용 archive include를 만들거나
  전시 원고를 그 파일로 옮기지 않는다.

## 기존 전시 수정

1. 대상 `exhibitions/<연도>.html`과 인접 연도 파일을 읽어 front matter, 전시 정보,
   갤러리 구조를 확인한다.
2. 제목, 날짜, 장소, 설명, 사진 항목을 해당 연도 파일 안에서만 필요한 범위로 수정한다.
   `exhibition_year`와 기존 섹션·문서 ID는 특별한 이유 없이 바꾸지 않는다.
3. 갤러리 사진은 인접한 항목과 같은 `figure`, 링크, 이미지, 캡션 구조와 접근성 속성을
   사용한다. HTML 들여쓰기는 공백 2칸으로 맞추고 탭을 쓰지 않는다.

## 새 연도 또는 전시 추가

1. 비슷한 전시 구성의 기존 연도 파일을 기준으로 `exhibitions/<연도>.html`을 만들고,
   숫자형 `exhibition_year`와 전시 원고·갤러리를 파일 안에 직접 작성한다.
2. 새 연도를 표시해야 하면 `_includes/exhibition_period_nav.html`에 해당 페이지 링크를
   최신순으로 추가한다.
3. 새 연도가 가장 최신 전시라면 `_includes/header.html`의 Exhibitions 진입 링크도 새
   페이지로 갱신한다. 기존 최신 페이지를 유지해야 하는 이유가 있으면 사용자 확인 없이
   바꾸지 않는다.
4. 한 해에 여러 전시가 있으면 기존 해당 연도 파일 안에 전시 섹션을 추가하고, 파일을
   임의로 전시별로 나누지 않는다.

## 이미지와 링크

- 전시 사진은 Azure Storage의 공개 URL을 사용하며 원본 이미지를 저장소에 추가하지 않는다.
- 전시 사진의 신규 업로드가 필요하면 연결 문자열을 코드·명령행·로그·채팅에 노출하지
  않는다. `work-image-upload` 스킬은 Work 작품 전용이므로 전시 사진에는 사용하지 않는다.
- 원본 또는 큰 이미지를 여는 링크와 축소 이미지는 인접 전시의 URL·속성 규칙을 따른다.
  이미지 URL, 대체 텍스트, 캡션을 추측해 만들지 않는다.

## 확인과 마무리

1. 시작 전 작업 트리 상태를 확인하고, 안전할 때만 `origin/main`을 fetch한 뒤
   `git pull --ff-only origin main`을 실행한다. `main` 이외의 브랜치를 만들거나 전환하지
   않는다.
2. 수정 뒤 `git diff --check`로 공백 오류를 확인하고, `rg`로 삭제된
   `exhibitions_archive.html` 참조가 생기지 않았는지 점검한다.
3. Jekyll build를 실행한다. 갤러리·내비게이션·헤더를 바꿨다면 로컬 사이트에서 해당
   전시 페이지, 연도 내비게이션, 이미지 링크와 레이아웃을 직접 확인한다.
4. 요청 범위의 diff만 남았는지 확인한 뒤, 짧은 한 줄 한국어 메시지로 commit·push한다.
   GitHub 인증·권한 문제는 우회하지 말고 결과를 알린다.
