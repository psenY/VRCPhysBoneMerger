# VRC PhysBone Merger (���Ǻϲ���ѹ������)

[![VRChat](https://img.shields.io/badge/VRChat-Avatar%203.0-blue.svg)](https://vrchat.com)
[![License](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)

һ��רΪ VRChat Avatar 3.0 ��Ƶĸ����ܡ�**���ƻ��� (Non-Destructive)** ���Ǻϲ����Ż����ߡ�֧��������ϸ�ƥ�䡢������ײ��ȥ�ء�ʵʱ���ܵȼ�Ԥ�⼰�ϴ�ʱ�������Զ�������

---

## ?? �������� (Key Features)

- ?? **���ƻ��Թ����� (Non-Destructive Workflow)**��
  - ���� `PhysBoneAutoMerger` �����ģ��Դ�ļ���Prefabs ������ 100% ԭ�����䡣
  - �ڵ�� VRChat �ϴ������ Play ����ʱ�����ڴ���ʱ�������Զ��ϲ����ϴ����Զ����ٱ�ǣ����׶ž� Missing Script �����
- ??? **������ִ����ȫ��ܼ��� (Order 999999)**��
  - �� NDMF��Modular Avatar��VRCFury ���沶��ܣ�Triturbo FaceTracking �ȣ���ȫ�����궯������ִ�кϲ����ž���������ʧЧ�� NullReferenceException ������
- ?? **�༶����ϵͳ (Strategy Presets)**��
  - **Strict (������ϸ���� - �Ƽ�)**�����ϲ������ָ���������ȫһ�µ�ͬ�㼶���ǣ�����Ӱ�춯Ч��
  - **Aggressive (��������)**������΢С�ݲ����ȼ��ٶ���������
  - **Custom (�Զ������)**��֧�����ɵ�����ֵ�ݲ�����ݲ������ת��˵�ȡ�
- ?? **���ܵȼ�ʵʱԤ�� (Performance Rank Preview)**��
  - ʵʱ����ģ�͵�ǰ��Ԥ��Ķ���������PhysBone Components & Transforms & Colliders����ֱ��չʾ Very Poor -> Poor / Medium / Good �仯��
- ?? **��ײ������ȥ������������ (Collider Deduplication & Cleanup)**��
  - �Զ�ȥ�غϲ��󶯹��б��е��ظ���ײ�壬����������ƻ���δ��Ч���������á�
- ?? **ԭ����Ӣ˫���޷��л� (Bilingual UI)**��
  - ֧���ڴ�����˵�����ʱһ���л�����������Ӣ�ġ�

---

## ?? ��װ��ʽ (Installation)

### ��ʽ 1��ͨ�� Unity Package Manager (UPM) ������� (�Ƽ�)
1. �� Unity �����˵���`Window` -> `Package Manager`��
2. ������Ͻǵ� `+` �ţ�ѡ�� **"Add package from disk..."**��
3. ѡ�б������Ŀ¼�µ� `package.json` ������ɰ�װ��

### ��ʽ 2���޸� Packages/manifest.json
�ڹ��̵� `Packages/manifest.json` �� `dependencies` ����ӣ�
```json
"pseny7.vrc.physbone-merger": "file:C:/Users/psenY7/Downloads/VRCPhysBoneMerger"
```

---

## ?? ʹ��ָ�� (Usage)

### 1. ���ƻ����Զ��ϲ����Ƽ���
1. ѡ�г����е� Avatar ���ڵ㡣
2. ��� Inspector �·��� `Add Component` -> ��������� `PhysBone Auto Merger (���Ƿ��ƻ����Զ��ϲ����)`��
3. �ڲ�����������ѡ�� **Strict (�ϸ����)** ���Զ�����ԡ�
4. ������� VRChat SDK �� **Build & Publish** ����� **Play ģʽ**��������Զ���ɺϲ���Դ�ļ��������

### 2. ����ʽ���ӻ��ϲ�����
1. ��� Unity �����˵���`Tools` -> `VRC ���Ǻϲ��� (PhysBone Merger)`��
2. ��ģ������ Avatar ��λ��
3. ��� **"ɨ�趯�ǲ�νṹ"**�����ϲ���ѡ�鼰�������ֱ仯��
4. ��� **"���ϲ�ѡ����"**��֧��һ������ (Undo)��

---

## ?? ��Դ��� (License)
����Ŀ���� [GPL-3.0 License](LICENSE) ��Դ��

