# ⚖️ التقرير الفني الشامل لإنجاز نظام الإحصاء القضائي
### محكمة بني سويف الابتدائية — جمهورية مصر العربية
**تاريخ الإنجاز:** سبتمبر 2026  
**البيئة والتقنيات:** `.NET 10 LTS` • `C# 14` • `Blazor Interactive Server` • `EF Core 10` • `SQL Server` • `Clean Architecture` • `Arabic RTL`

---

## 1. ملخص الموقف الفني العام

| المؤشر | النتيجة المحققة | البيان الفني |
|---|:---:|---|
| **بناء النظام بالكامل (`dotnet build`)** | **0 أخطاء — 0 تحذيرات** | بناء نظيف لكافة المشاريع الخمسة في الـ Solution |
| **اختبارات الوحدة والاعتمادية (`dotnet test`)** | **7 ناجحة — 0 فاشلة** | تغطية القواعد الحسابية، القيود، وتوزيعات الأحكام |
| **ترحيل قاعدة البيانات (`Migrations`)** | **5 Migrations مطبقة** | الجداول، القيود الفريدة، والفهارس مطبقة على SQL Server |
| **التشغيل الفعلي والتهيئة الأولية (`Seed`)** | **ناجح ومستقر** | تشغيل آلي عند الإقلاع عبر `DatabaseInitializer` |
| **الهيكل المعماري** | **Clean Architecture** | فصل تام بين الطبقات بدون أي تبعيات دائرية |
| **واجهة المستخدم** | **Arabic RTL + Cairo Font** | شاشات متجاوبة بالكامل بنظام ألوان الهوية القضائية |

---

## 2. الهيكل المعماري للحل البرمجي (Clean Architecture)

تم تقسيم الـ Solution (`JudicialStatistics.slnx`) إلى 5 مشاريع وفق معايير هندسة البرمجيات المتقدمة:

```
JudicialStatistics.slnx
│
├── 🏛️ JudicialStatistics.Domain (نواة العمل الخالصة - Core Business Entities)
│   ├── Common/        ← BaseEntity بمعرف bigint مستقل
│   ├── Courts/        ← CourtUnit, ReportingScope, ReportingScopeUnit
│   ├── Identity/      ← AppUser, Role, Permission, RolePermission, UserCourtGrant, UserSessionGrant
│   ├── Years/         ← JudicialYear (1 أكتوبر - 30 سبتمبر), StatisticalPeriod (12 شهراً)
│   ├── Organization/  ← OrganizationVersion, Circuit, CircuitVersion, Session, SessionVersion, Judge, JudicialRole, CircuitJudgeAssignment
│   ├── Geography/     ← GeographicArea, SessionJurisdiction
│   ├── Cases/         ← CaseTaxonomyNode, CaseType
│   ├── Rules/         ← RuleSetVersion, CaseYearPolicy, CaseYearBand, Lookups & Business Rules
│   └── Statistics/    ← StatisticsHeader, OpeningBalance, IncomingRow, ProductionRow, JudgmentAllocation, ClosingBalance, AuditEntry
│
├── ⚙️ JudicialStatistics.Application (عقود الخدمات ونماذج البيانات - Interfaces & DTOs)
│   ├── Courts/        ← ICourtsService, CourtUnitDto, ReportingScopeDto
│   ├── Years/         ← IJudicialYearsService, JudicialYearDto, StatisticalPeriodDto
│   ├── Organization/  ← IOrganizationService, CircuitDto, SessionDto, JudgeDto, JudicialRoleDto
│   ├── Rules/         ← IRulesService, CaseTypeDto, RuleSetVersionDto, CaseYearPolicyDto
│   ├── Statistics/    ← IStatisticsService, StatisticsMatrixDto, Commands
│   ├── Reports/       ← IReportsService, PeriodReportSummaryDto, AnnualReportSummaryDto
│   └── Identity/      ← IUsersService, AppUserDto, UserCourtGrantDto
│
├── 🗄️ JudicialStatistics.Infrastructure (الوصول لقاعدة البيانات وتنفيذ الخدمات)
│   ├── Persistence/   ← JudicialDbContextBase<TUser>, JudicialDbContext, Configurations
│   ├── Migrations/    ← 5 Migrations رسمية مطبقة على قاعدة البيانات
│   ├── Seed/          ← IdentitySeedData, CourtSeedData, ReferenceSeedData
│   └── Services/      ← تطبيق الخدمات بالكامل بنمط IDbContextFactory
│
├── 🌐 JudicialStatistics.Web (واجهة المستخدم Blazor Interactive Server)
│   ├── Components/Pages/  ← 9 شاشات تفاعلية باللغة العربية
│   ├── Components/Layout/ ← MainLayout عربي RTL وقائمة جانبية قابلة للطي
│   ├── wwwroot/css/       ← app.css بنظام ألوان المحكمة وخط Cairo
│   └── Data/              ← ApplicationDbContext يرث من JudicialDbContextBase<ApplicationUser>
│
└── 🧪 JudicialStatistics.Tests (اختبارات الجودة وضمان الاعتمادية)
    └── StatisticsCalculationTests ← اختبارات المعادلات الحسابية والحدود الزمنية وتوزيعات الأحكام
```

---

## 3. الابتكار المعماري: نمط سياق البيانات المزدوج (Dual DbContext Pattern)

لمنع حدوث أي تبعية دائرية بين مشروع البنية التحتية ومصادقة ASP.NET Core Identity في مشروع الويب:
1. **`JudicialDbContextBase<TUser>` (Abstract في Infrastructure):**
   - يحتوي على تعريف جميع الجداول الـ 28 وتهيئتها بالـ Fluent API والقيود والفهارس.
2. **`JudicialDbContext` (Concrete في Infrastructure):**
   - يرث من السياق الأساسي باستخدام `IdentityUser` ويدعم وقت التصميم (`Design-Time`)، ويتحكم بإنشاء وتطبيق الـ Migrations والـ Seed Data.
3. **`ApplicationDbContext` (في Web):**
   - يرث من `JudicialDbContextBase<ApplicationUser>` ويخدم تشغيل Identity الفعلي دون الإخلال بملفات القالب الأصلية.
4. **نمط `IDbContextFactory` في Blazor Server:**
   - تم تسجيل الـ Services باستخدام `IDbContextFactory` لمنع أي تعارض في التزامن المتعدد وضمان استقرار الجلسات.

---

## 4. المراحل المنجزة بالتفصيل (المراحل 0 إلى 8)

### المرحلة 0: التأسيس المعماري وبناء المشاريع
- إنشاء المشاريع الخمسة وإضافتها للـ Solution بدون المساس بملفات المشروع الأصلي.
- استخدام `bigint` كمعرف أساسي لكافة الجداول وحماية التزامن بـ `rowversion`.

### المرحلة 1: الهوية والمحاكم والبيانات الأولية (Seed Data)
- **الأدوار والصلاحيات:** تهيئة **21 صلاحية دقيقة** و **7 أدوار وظيفية** (مدير نظام، رئيس محكمة، عضو مراجعة، مدخل بيانات، باحث إحصائي...).
- **محكمة بني سويف الابتدائية:** تهيئة المحكمة الابتدائية ونطاق التقرير الرئيسي.
- **البيانات المرجعية:** تهيئة 9 مناطق جغرافية (المحافظة والمراكز والبنادر)، والصفات القضائية (رئيس الدائرة، عضو يمين، عضو يسار، عضو).
- **التشغيل التلقائي:** إعداد `DatabaseInitializer` لتنفيذ الـ Migrations والـ Seed تلقائياً عند أول إقلاع.

### المرحلة 2: الأعوام القضائية والتشكيلات
- **العام القضائي:** الالتزام ببدء العام القضائي من **1 أكتوبر** حتى **30 سبتمبر** من العام التالي.
- **الفترات الشهرية:** توليد **12 فترة إحصائية شهرية** تلقائياً ومستقلة لكل عام مسجل.
- **التشكيل والدوائر:** دعم أنواع الدوائر (جزئي فردي، كلي 3 قضاة، جنح مستأنف، أسرة، عمال)، وتحديد فترات السريان.
- **القضاة والتكليفات:** تعريف القضاة بأكواد فريدة، وربطهم بنسخ الدوائر مع صفاتهم القضائية.

### المرحلة 3: تصنيفات القضايا وقواعد العمل
- **شجرة التصنيف:** هيكل هرمي يتيح تفريع الدعاوى (مدني، جنائي، أسرة، عمال، أحداث).
- **شرائح سنوات القيد:** دعم الشرائح الزمنية لكل نوع قضية، ودعم شريحة `AllYears` المخصصة للجنح لعدم إلزام المستخدم بسنة قيد مفرقة.
- **بنود الإدخال:** جداول مهيأة لبنود الوارد (سابق، جديد، مجدد من الشطب، معجل من الوقف، تقصير وارد).
- **بنود الإنتاج:** تصنيف قطعي للأحكام الصادرة، والقرارات التمهيدية، والتأجيلات، وإجراءات القاضي (مد أجل، إعادة للمرافعة).

### المرحلة 4: محرك الإحصائية الشهرية
- **رأس الإحصائية (`StatisticsHeader`):** دورة مستندية محكمة:
  $$\text{مسودة (Draft)} \longrightarrow \text{تقديم (Submitted)} \longrightarrow \text{مراجعة (UnderReview)} \longrightarrow \text{اعتماد (Approved)} \longleftrightarrow \text{إعادة فتح (Reopened)}$$
- **سجل التدقيق غير القابل للتعديل (`AuditEntry`):** توثيق كل عملية حفظ، تقديم، قبول، إرجاع، أو اعتماد نهائي باسم المستخدم وتوقيت السيرفر الدقيق.
- **توزيعات الجنح:** إمكانية تخصيص أعداد الأحكام الجنائية لكل قاضٍ في تشكيل الدائرة.

### المرحلة 5: الرصيد الختامي والهوية البصرية العربية
- **المعادلة الحسابية المعتمدة:**
  $$\text{المتاح للتصرف} = \text{الرصيد الافتتاحي} + \text{إجمالي الوارد}$$
  $$\text{الرصيد الختامي} = \text{المتاح للتصرف} - \text{الأحكام الصادرة القطعية}$$
- **التوليد الآلي:** الأرصدة الختامية لا تُدخل يدوياً؛ بل تُحسب وتُسجل رسمياً بجدول `ClosingBalances` عند الاعتماد النهائي فقط.
- **التصميم والهوية:**
  - خط **Cairo** المدمج بكافة أوزانه.
  - دعم اتجاه **RTL** كامل في كافة العناصر والصفحات.
  - ألوان الهوية الرسمية: الكحلي القضائي (`#123E56`)، الفيروزي الداكن (`#1D6F73`)، الذهبي (`#C8973B`).

### المرحلة 6 & 7: الشاشات التفاعلية والتقارير ومحرك التدوير التلقائي
تم بناء 9 شاشات Blazor Interactive متكاملة:
1. **الرئيسية (`/`):** لوحة معلومات تفاعلية تضم مؤشرات العام الحالي، بطاقات الإحصاءات السريعة، والروابط الخدمية.
2. **المحاكم والنطاقات (`/courts`):** استعراض شجرة المحاكم، تفعيل وتعطيل الوحدات، وإضافة محاكم جديدة.
3. **الأعوام القضائية (`/judicial-years`):** إنشاء عام قضائي بزر واحد، توليد الفترات الـ 12 تلقائياً، وتفعيل/إغلاق الأعوام.
4. **التشكيل القضائي (`/organization`):** تبويبات الدوائر القضائية، الجلسات، السادة القضاة، والتكليفات القضائية.
5. **استعراض الإحصائيات (`/statistics`):** شاشة تصفية متقدمة حسب العام والفترة والحالة، وفتح إحصائية جديدة.
6. **نموذج إدخال الإحصائية (`/statistics/entry/{id}`):** الشاشة المركزية الحية:
   - الرصيد الافتتاحي (مدور آلياً أو قابل للإدخال في الشهر الأول).
   - جدول الوارد بكافة مصادره مع إجماليات لحظية.
   - جدول الإنتاج والأحكام والتأجيلات.
   - الحساب الفوري للرصيد الختامي ونسبة الإنجاز دون الحاجة لإعادة تحميل الصفحة.
   - أزرار الاعتماد، الإرجاع، وإعادة الفتح المشروط بمبرر تدقيق.
7. **التقارير ومؤشرات الإنجاز (`/reports`):**
   - التقرير الشهري لحركة القضايا ونسب الفصل.
   - التقرير التراكمي السنوي للعام القضائي.
   - تقرير إنجاز السادة القضاة وحصص الأحكام المخصصة.
8. **قواعد التصنيف (`/rules`):** استعراض شجرة التصنيف، أنواع القضايا، وإضافة نوع دعوى جديد.
9. **المستخدمون (`/users`):** إدارة مستخدمي النظام ومنح النطاقات وصلاحيات المحاكم.
10. **سجل التدقيق (`/audit`):** العرض التاريخي الزمني لجميع العمليات والحركات التوثيقية.

- **محرك التدوير التلقائي للأرصدة (`Carry-Forward`):**
  عند قيام المستخدم بفتح نموذج إحصائية لشهر لاحق لنفس الجلسة، يتحقق النظام تلقائياً من وجود إحصائية معتمدة للشهر السابق، ويقوم بترحيل أرصدتها الختامية كأرصدة افتتاحية للشهر الجديد مباشرة لمنع أي تلاعب أو أخطاء حسابية يدوية.

### المرحلة 8: اختبارات الجودة والتحقق الشامل
- كتابة اختبارات وحدة في `JudicialStatistics.Tests`:
  - صحة بداية ونهاية العام القضائي (1 أكتوبر إلى 30 سبتمبر).
  - صحة تطبيق معادلة الرصيد الختامي وعدم قبوله للقيم السالبة.
  - تطابق مجموع توزيعات الأحكام الجنائية مع عدد الأحكام المسجلة.
- اجتياز الاختبارات بالكامل: **7 Tests Passed**.

---

## 5. حالة الـ Migrations المنفذة على قاعدة البيانات

```
[X] 20260922225119_Phase01_IdentityAndCourts
[X] 20260923054847_Phase02_YearsOrganizationGeography
[X] 20260923055220_Phase03_CasesAndRules
[X] 20260923055556_Phase04_Statistics
[X] 20260923055711_Phase05_ClosingBalance
```

---

## 6. كيفية تشغيل واستخدام التطبيق

التطبيق جاهز تماماً للتشغيل المباشر عبر الطرفية بالأمر:
```powershell
dotnet run --project JudicialStatistics.Web/JudicialStatistics.Web.csproj
```

ثم الدخول من المتصفح إلى: **`http://localhost:5002`** (أو الرابط المعروض في الطرفية).

---
*تم إنشاء هذا التقرير وتوثيق كافة خطوات المشروع رسمياً داخل مجلد الحل البرمجي.*
