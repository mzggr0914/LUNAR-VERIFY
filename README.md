# LUNAR VERIFY
단일서버를 위한 디스코드 인증봇입니다.  
C#으로 만들어졌으며 빌드관련 질문은 받지 않습니다.

# 지원 기능
```diff
OAuth2 유튜브 채널 인증 지원  
OAuth2 이메일 인증 지원  
유튜브 구독인증 지원  
유튜브 채널 링크 인증 지원  
일반 버튼 인증 지원  

! 사용자의 채널아이디 또는 이메일은 Firebase에 보관됩니다.

- 이는 사용자 중복가입을 방지하기 위함이며 사용자의 정보는 관리자에게만 전송됩니다.
```
# 사용법
```diff
@@ Firebase.json 파일에 비공개키 내용을 추가한 뒤 아래의 내용을 db에 적용해야 합니다. @@

- 모든 필드의 형식은 string입니다.

Config:
  ├─Bot
  │  └─TOKEN
  ├─Server
  │  └─AdminId (관리자 아이디)
  │  └─ServerId (이용할 디스코드 서버 아이디)
  ├─Youtube
  │  └─ClientId (GCP OAuth2 Client Id)
  │  └─GCPAPIKEY (Youtube Data API V3 와 People API사용 권한이 있는 GCP API KEY)
  │  └─SubRoleId (구독인증을 했을때 지급될 역할 ID)
  │  └─URL (리다이렉트 URL)
  │  └─UserRoleId (인증을 했을때 지급될 역할 ID)
  │  └─YoutubeChannelId (본인의 유튜브 채널 아이디)

! Youtube 문서는 해당 기능을 사용하지 않을 것이어도 필드가 비어있으면 오류가 발생하니 공백문자라도 추가해야 합니다.
```

# Credit
디스코드 봇 사용을 위한 라이브러리 : discord-net/Discord.Net  
웹 사이트를 열기위한 라이브러리 : dajuric/simple-http
Firestore db사용을 위한 라이브러리 : Google.Cloud.Firestore
<br/>
<br/>  
<br/>
discord : `.lunarlight`
