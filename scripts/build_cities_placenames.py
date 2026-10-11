#!/usr/bin/env python3
"""Build complete Chinese placenames dictionary for all 4287 entries in Cities/0000_0000.wwp."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = Path("G:/soft/steam/steamapps/common/Command - Modern Operations")

sys.path.insert(0, str(ROOT / "scripts"))
from wwp import load, save  # noqa: E402

# 1. Authoritative Major World Capitals, Military Ports & Strategic Cities
MAJOR_CITIES: dict[str, str] = {
    # Middle East / Mediterranean / Persian Gulf
    "Nicosia": "尼科西亚", "Limassol": "利马索尔", "Larnaca": "拉纳卡", "Paphos": "帕福斯",
    "Famagusta": "法马古斯塔", "Kyrenia": "凯里尼亚",
    "Latakia": "拉塔基亚", "Tartus": "塔尔图斯", "Hama": "哈马", "Homs": "霍姆斯",
    "Hamah": "哈马", "Damascus": "大马士革", "Aleppo": "阿勒颇", "Idlib": "伊德利卜", "Douma": "杜马",
    "Beirut": "贝鲁特", "Tripoli": "的黎波里", "Sidon": "赛达", "Tyre": "提尔", "Nabatiye et Tahta": "奈拜提耶",
    "Jerusalem": "耶路撒冷", "West Jerusalem": "西耶路撒冷", "East Jerusalem": "东耶路撒冷",
    "Tel Aviv": "特拉维夫", "Yafo": "雅法", "Haifa": "海法", "Ashdod": "阿什杜德", "Ashqelon": "阿什凯隆",
    "Beersheba": "贝尔谢巴", "Netanya": "内坦亚", "Holon": "霍隆", "Petah Tiqwa": "佩塔提克瓦",
    "Ramat Gan": "拉马特甘", "Rishon LeZiyyon": "里雄莱锡安", "Bat Yam": "巴特亚姆",
    "Gaza": "加沙", "Rafah": "拉法", "Khan Yunis": "汗尤尼斯", "Hebron": "希伯伦", "Nablus": "纳布卢斯",
    "Amman": "安曼", "Zarqa": "扎尔卡", "Irbid": "伊尔比德", "Russeifa": "鲁赛法", "Aqaba": "亚喀巴",
    "Port Said": "塞得港", "Suez": "苏伊士", "Alexandria": "亚历山大", "Cairo": "开罗",
    "Damanhur": "达曼胡尔", "Al Mansurah": "曼苏拉", "Ismailia": "伊斯梅利亚", "Arish": "阿里什",
    "Adana": "阿达纳", "Antakya": "安塔基亚", "Iskenderun": "伊斯肯德伦", "Mersin": "梅尔辛",
    "Mercin": "梅尔辛", "Gaziantep": "加济安泰普", "Antalya": "安塔利亚", "Konya": "科尼亚",
    "Ankara": "安卡拉", "Istanbul": "伊斯坦布尔", "Izmir": "伊兹密尔", "Bursa": "布尔萨",
    "Baghdad": "巴格达", "Basra": "巴士拉", "Mosul": "摩苏尔", "Erbil": "埃尔比勒", "Kirkuk": "基尔库克",
    "Tehran": "德黑兰", "Isfahan": "伊斯法罕", "Shiraz": "设拉子", "Tabriz": "大不里士", "Mashhad": "马什哈德",
    "Bandar Abbas": "阿巴斯港", "Bushehr": "布什尔", "Chabahar": "恰巴哈尔",
    "Riyadh": "利雅得", "Jeddah": "吉达", "Mecca": "麦加", "Medina": "麦地那", "Dammam": "达曼",
    "Kuwait": "科威特城", "Manama": "麦纳麦", "Doha": "多哈", "Abu Dhabi": "阿布扎比", "Dubai": "迪拜",
    "Muscat": "马斯喀特", "Sanaa": "萨那", "Aden": "亚丁",

    # East Asia / China / Taiwan
    "Beijing": "北京", "Shanghai": "上海", "Tianjin": "天津", "Chongqing": "重庆",
    "Guangzhou": "广州", "Shenzhen": "深圳", "Hong Kong": "香港", "Macau": "澳门",
    "Taipei": "台北", "Kaohsiung": "高雄", "Taichung": "台中", "Tainan": "台南",
    "Keelung": "基隆", "Hsinchu": "新竹", "Chiayi": "嘉义", "Taoyuan": "桃园",
    "Nanjing": "南京", "Hangzhou": "杭州", "Wuhan": "武汉", "Chengdu": "成都",
    "Xi'an": "西安", "Shenyang": "沈阳", "Dalian": "大连", "Qingdao": "青岛",
    "Jinan": "济南", "Xiamen": "厦门", "Fuzhou": "福州", "Changsha": "长沙",
    "Zhengzhou": "郑州", "Harbin": "哈尔滨", "Changchun": "长春", "Shijiazhuang": "石家庄",
    "Kunming": "昆明", "Guiyang": "贵阳", "Nanning": "南宁", "Haikou": "海口",
    "Sanya": "三亚", "Lanzhou": "兰州", "Xining": "西宁", "Yinchuan": "银川",
    "Urumqi": "乌鲁木齐", "Lhasa": "拉萨", "Hohhot": "呼和浩特", "Baotou": "包头",
    "Wenzhou": "温州", "Ningbo": "宁波", "Suzhou": "苏州", "Wuxi": "无锡",
    "Changzhou": "常州", "Nantong": "南通", "Yangzhou": "扬州", "Zhenjiang": "镇江",
    "Huzhou": "湖州", "Jiaxing": "嘉兴", "Shaoxing": "绍兴", "Jinhua": "金华",
    "Taizhou": "台州", "Zhoushan": "舟山", "Quanzhou": "泉州", "Zhangzhou": "漳州",
    "Shantou": "汕头", "Zhanjiang": "湛江", "Zhuhai": "珠海", "Foshan": "佛山",
    "Dongguan": "东莞", "Zhongshan": "中山", "Jiangmen": "江门", "Zhaoqing": "肇庆",
    "Huizhou": "惠州", "Chaozhou": "潮州", "Jieyang": "揭阳", "Maoming": "茂名",
    "Yangjiang": "阳江", "Qinzhou": "钦州", "Beihai": "北海", "Fangchenggang": "防城港",
    "Yantai": "烟台", "Weihai": "威海", "Rizhao": "日照", "Lianyungang": "连云港",
    "Yingkou": "营口", "Dandong": "丹东", "Huludao": "葫芦岛", "Jinzhou": "锦州",
    "Panjin": "盘锦", "Tangshan": "唐山", "Cangzhou": "沧州", "Qinhuangdao": "秦皇岛",
    "Zunyi": "遵义", "Zoucheng": "邹城", "Zhuzhou": "株洲", "Zhumadian": "驻马店",
    "Shangqiu": "商丘", "Zhuji": "诸暨", "Zhoukou": "周口", "Zhoucun": "周村",
    "Zibo": "淄博", "Anyang": "安阳", "Zaozhuang": "枣庄", "Zaoyang": "枣阳",
    "Yuyao": "余姚", "Kunshan": "昆山", "Yunfu": "云浮", "Yuncheng": "运城",
    "Yulin": "榆林", "Yuci": "榆次", "Qianjiang": "潜江", "Zhongxiang": "钟祥",
    "Yichun": "宜春", "Yichang": "宜昌", "Yangshuo": "阳朔", "Yangquan": "阳泉",
    "Yancheng": "盐城", "Shangrao": "上饶", "Xinzhou": "忻州", "Xinyang": "信阳",
    "Xingtai": "邢台", "Ankang": "安康", "Xiaogan": "孝感", "Xianyang": "咸阳",
    "Xiantao": "仙桃", "Changzhi": "长治",
    "Kashi": "喀什", "Jiayuguan": "嘉峪关", "Hotan": "和田", "UEruemqi": "乌鲁木齐",
    "Laochenglu": "老城路", "Shihezi": "石河子", "Sayibage": "沙依巴克", "Hami": "哈密",
    "Changji": "昌吉", "Aral": "阿拉尔", "Altay": "阿勒泰", "Yingbazha": "英巴扎",
    "Zigong": "自贡", "Mizhou": "密州", "Yuxi": "玉溪", "Zhicheng": "枝城",
    "Zhenzhou": "真州", "Xinghua": "兴化", "Zhaotong": "昭通", "Luofeng": "罗峰",
    "Chengzhong": "城中", "Qingzhou": "青州", "Yibin": "宜宾", "Tongshan": "铜山",
    "Jiangguanchi": "蒋官池", "Xuanzhou": "宣州", "Xiuying": "秀英", "Xiulin": "秀林",
    "Nangandao": "南竿岛", "Xintai": "新泰", "Xinpu": "新浦", "Xinji": "辛集",
    "Xindi": "新堤", "Sanshui": "三水", "Xichang": "西昌", "Xiazhen": "夏镇",
    "Xianning": "咸宁", "Xiangtan": "湘潭", "Xiangyang": "襄阳", "Wuzhou": "梧州",
    "Wuxue": "武穴", "Dongyang": "东阳", "Changde": "常德", "Wuhu": "芜湖",
    "Wuhai": "乌海", "Wuda": "乌达", "Tianfu": "天府", "Weinan": "渭南",
    "Weifang": "潍坊", "Wanxian": "万县", "Wafangdian": "瓦房店", "Tongzhou": "通州",
    "Wusong": "吴淞", "Tianshui": "天水", "Tengzhou": "滕州", "Tanggu": "塘沽",
    "Taiyuan": "太原", "Taishan": "台山", "Tai'an": "泰安", "Suizhou": "随州",
    "Suining": "遂宁", "Songjiang": "松江", "Shizuishan": "石嘴山", "Shiyan": "十堰",
    "Shiqiao": "市桥", "Shiqi": "石岐", "Shilong": "石龙", "Tongchuan": "铜川",
    "Shashi": "沙市", "Shaping": "沙坪", "Shaowu": "邵武", "Shaoguan": "韶关",
    "Shanwei": "汕尾", "Shahecheng": "沙河城", "Sanming": "三明", "Qujing": "曲靖",
    "Huai'an": "淮安", "Qingyuan": "清远", "Puyang Chengguanzhen": "濮阳城关镇",
    "Puyang": "濮阳", "Putian": "莆田", "Puqi": "蒲圻", "Pulandian": "普兰店",
    "Pingxiang": "萍乡", "Pingshan": "平山", "Pingliang": "平凉", "Pingdingshan": "平顶山",
    "Dadukou": "大渡口", "Neijiang": "内江", "Nanyang": "南阳", "Nanping": "南平",
    "Nanchong": "南充", "Nanchang": "南昌", "Mingshui": "明水", "Mianyang": "绵阳",
    "Mentougou": "门头沟", "Meizhou": "梅州", "Wuchuan": "吴川", "Majie": "马街",
    "Macheng": "麻城", "Maba": "马坝", "Luqiao": "路桥", "Luoyang": "洛阳",
    "Luohe": "漯河", "Kangding": "康定", "Luancheng": "栾城", "Loudi": "娄底",
    "Guankou": "关口", "Puning": "普宁", "Linyi": "临沂", "Linxia Chengguanzhen": "临夏城关镇",
    "Linxi": "林西", "Linshui": "邻水", "Qingnian": "青年", "Linhai": "临海",
    "Linfen": "临汾", "Licheng": "历城", "Liaocheng": "聊城", "Lianran": "连然",
    "Wuwei": "武威", "Lianjiang": "廉江", "Leshan": "乐山", "Lengshuijiang": "冷水江",
    "Leiyang": "耒阳", "Lecheng": "乐城", "Laohekou": "老河口", "Langfang": "廊坊",
    "Laiyang": "莱阳", "Laiwu": "莱芜", "Kaiyuan": "开远", "Kaifeng": "开封",
    "Jiujiang": "九江", "Qianzhou": "乾州", "Jining": "济宁", "Jingzhou": "荆州",
    "Jingmen": "荆门", "Jingling": "竟陵", "Jingdezhen": "景德镇", "Jinchang": "金昌",
    "Jincheng": "晋城", "Jieshou": "界首", "Jieshi": "碣石", "Jiazi": "甲子",
    "Jiaozuo": "焦作", "Jiaozhou": "胶州", "Ji'an": "吉安", "Guangyuan": "广元",
    "Humen": "虎门", "Huicheng": "惠城", "Xinhui": "新会", "Huangzhou": "黄州",
    "Huangyan": "黄岩", "Huangshi": "黄石", "Dasha": "大沙", "Huanggang": "黄冈",
    "Daxing": "大兴", "Huainan": "淮南", "Huaihua": "怀化", "Huaibei": "淮北",
    "Heze": "菏泽", "Yiyang": "益阳", "Hepo": "河婆", "Hengyang": "衡阳",
    "Hengshui": "衡水", "Hefei": "合肥", "Hebi": "鹤壁", "Hanzhong": "汉中",
    "Hangu": "汉沽", "Handan": "邯郸", "Jiaojiang": "椒江", "Haimen": "海门",
    "Guli": "古里", "Guilin": "桂林", "Guangshui": "广水", "Gejiu": "个旧",
    "Gaozhou": "高州", "Gaoping": "高平", "Gaomi": "高密", "Fuyang": "阜阳",
    "Fuling": "涪陵", "Ezhou": "鄂州", "Encheng": "恩城", "Shengli": "胜利",
    "Dongtai": "东台", "Donghai": "东海", "Dingzhou": "定州", "Dezhou": "德州",
    "Deyang": "德阳", "Lijiang": "丽江", "Dazhou": "达州", "Dawukou": "大武口",
    "Danshui": "淡水", "Daliang": "大良", "Dali": "大理", "Chuzhou": "滁州",
    "Yangchun": "阳春", "Chizhou": "池州", "Chenzhou": "郴州", "Jiangyin": "江阴",
    "Chenghua": "成华", "Chaohu": "巢湖", "Buhe": "埠河", "Bozhou": "亳州",
    "Boshan": "博山", "Binzhou": "滨州", "Bengbu": "蚌埠", "Beibei": "北碚",
    "Baoding": "保定", "Baiyin": "白银", "Shangyu": "上虞", "Anshun": "安顺",
    "Anqiu": "安丘", "Anqing": "安庆", "Anbu": "庵埠", "Jiangyou": "江油",
    "Lianghu": "两湖", "Yueyang": "岳阳", "Zhaodong": "肇东", "Zhangjiakou": "张家口",
    "Zalantun": "扎兰屯", "Yushu": "榆树", "Yanji": "延吉", "Yakeshi": "牙克石",
    "Zhangjiakou Shi Xuanhua Qu": "宣化", "Xilin Hot": "锡林浩特", "Ulanhot": "乌兰浩特",
    "Tongliao": "通辽", "Tieling": "铁岭", "Tieli": "铁力", "Guangming": "光明",
    "Sujiatun": "苏家屯", "Suihua": "绥化", "Siping": "四平", "Shunyi": "顺义",
    "Shuangyashan": "双鸭山", "Shuangcheng": "双城", "Shanhaiguan": "山海关",
    "Fendou": "奋斗", "Taihe": "太和", "Qiqihar": "齐齐哈尔", "Qianguo": "前郭",
    "Panshan": "盘山", "Nehe": "讷河", "Nanpiao": "南票", "Lianhe": "联合",
    "Mudanjiang": "牡丹江", "Longjing": "龙井", "Longjiang": "龙江", "Longfeng": "龙凤",
    "Liaoyuan": "辽源", "Liaoyang": "辽阳", "Jixi": "鸡西", "Jiutai": "九台",
    "Jiupu": "九铺", "Jishu": "吉舒", "Lianshan": "连山", "Jilin": "吉林",
    "Minzhu": "民主", "Jiamusi": "佳木斯", "Jalai Nur": "扎赉诺尔", "Jagdaqi": "加格达奇",
    "Hulan Ergi": "富拉尔基", "Hulan": "呼兰", "Huadian": "桦甸", "Honggang": "红岗",
    "Heihe": "黑河", "Hegang": "鹤岗", "Hailun": "海伦", "Hailar": "海拉尔",
    "Haicheng": "海城", "Gongzhuling": "公主岭", "Fuyu": "扶余", "Fuxin": "阜新",
    "Fushun": "抚顺", "Fengcheng": "凤城", "Dunhua": "敦化", "Dongling": "东陵",
    "Dongxing": "东兴", "Dehui": "德惠", "Datong": "大同", "Daqing": "大庆",
    "Chifeng": "赤峰", "Chengde": "承德", "Chaoyang": "朝阳", "Benxi": "本溪",
    "Beipiao": "北票", "Bei'an": "北安", "Baoshan": "保山", "Baicheng": "白城",
    "Baishan": "白山", "Anshan": "鞍山", "Anda": "安达", "Acheng": "阿城",
    "Changshu City": "常熟", "Jiashan": "嘉善", "Fenghuang": "凤凰",
    "Zhu Cheng City": "诸城", "Shangri-La": "香格里拉", "Ordos": "鄂尔多斯",
    "Wenshan City": "文山", "Liupanshui": "六盘水", "Tsuen Wan": "荃湾",
    "Yuen Long Kau Hui": "元朗", "Kowloon": "九龙",
    "Douliu": "斗六", "Banqiao": "板桥", "Nantou": "南投", "Hualian": "花莲",
    "Taoyuan City": "桃园", "Taitung City": "台东",

    # Japan / Korea / Asia-Pacific
    "Tokyo": "东京", "Yokohama": "横滨", "Osaka": "大阪", "Nagoya": "名古屋",
    "Sapporo": "札幌", "Fukuoka": "福冈", "Kobe": "神户", "Kyoto": "京都",
    "Hiroshima": "广岛", "Sendai": "仙台", "Nagasaki": "长崎", "Okinawa": "冲绳",
    "Naha": "那霸", "Sasebo": "佐世保", "Yokosuka": "横须贺", "Kure": "吴市",
    "Maizuru": "舞鹤", "Ominato": "大凑", "Hakodate": "函馆",
    "Seoul": "首尔", "Busan": "釜山", "Incheon": "仁川", "Daegu": "大邱",
    "Daejeon": "大田", "Gwangju": "光州", "Ulsan": "蔚山", "Pohang": "浦项",
    "Jinhae": "镇海", "Mokpo": "木浦", "Jeju": "济州",
    "Pyongyang": "平壤", "Nampo": "南浦", "Wonsan": "元山", "Chongjin": "清津",
    "Hamhung": "咸兴", "Sinuiju": "新义州", "Kaesong": "开城",
    "Hanoi": "河内", "Haiphong": "海防", "Da Nang": "岘港", "Cam Ranh": "金兰",
    "Ho Chi Minh City": "胡志明市", "Saigon": "西贡", "Vung Tau": "头顿",
    "Manila": "马尼拉", "Subic": "苏比克", "Cebu": "宿务", "Davao": "达沃",
    "Bangkok": "曼谷", "Sattahip": "萨塔希普", "Phuket": "普吉",
    "Singapore": "新加坡", "Kuala Lumpur": "吉隆坡", "Penang": "槟城",
    "Jakarta": "雅加达", "Surabaya": "泗水", "Bandung": "万隆",
    "Sydney": "悉尼", "Melbourne": "墨尔本", "Brisbane": "布里斯班",
    "Perth": "珀斯", "Adelaide": "阿德莱德", "Darwin": "达尔文",
    "Auckland": "奥克兰", "Wellington": "惠灵顿", "Honolulu": "檀香山", "Pearl Harbor": "珍珠港",
    "Guam": "关岛", "Apra": "阿普拉港",

    # Europe / Russia
    "Moscow": "莫斯科", "Saint Petersburg": "圣彼得堡", "Murmansk": "摩尔曼斯克",
    "Severomorsk": "北莫尔斯克", "Arkhangelsk": "阿尔汉格尔斯克", "Severodvinsk": "北德文斯克",
    "Kaliningrad": "加里宁格勒", "Baltiysk": "波罗的斯克", "Sevastopol": "塞瓦斯托波尔",
    "Novorossiysk": "新罗西斯克", "Vladivostok": "符拉迪沃斯托克", "Petropavlovsk": "堪察加彼得罗巴甫洛夫斯克",
    "London": "伦敦", "Portsmouth": "朴茨茅斯", "Plymouth": "普利茅斯", "Glasgow": "格拉斯哥",
    "Edinburgh": "爱丁堡", "Belfast": "贝尔法斯特", "Gibraltar": "直布罗陀",
    "Paris": "巴黎", "Toulon": "土伦", "Brest": "布雷斯特", "Cherbourg": "瑟堡",
    "Berlin": "柏林", "Kiel": "基尔", "Wilhelmshaven": "威廉港", "Hamburg": "汉堡",
    "Rome": "罗马", "Taranto": "塔兰托", "Naples": "那不勒斯", "La Spezia": "拉斯佩齐亚",
    "Madrid": "马德里", "Rota": "罗塔", "Cartagena": "卡塔赫纳", "Barcelona": "巴塞罗那",
    "Lisbon": "里斯本", "Athens": "雅典", "Piraeus": "比雷埃夫斯", "Souda": "苏达湾",
    "Warsaw": "华沙", "Gdynia": "格丁尼亚", "Gdansk": "格但斯克",
    "Stockholm": "斯德哥尔摩", "Karlskrona": "卡尔斯克鲁纳", "Gothenburg": "哥德堡",
    "Oslo": "奥斯陆", "Bergen": "卑尔根", "Haakonsvern": "哈康斯文", "Tromso": "特罗姆瑟",
    "Helsinki": "赫尔辛基", "Copenhagen": "哥本哈根", "Reykjavik": "雷克雅未克",

    # Americas
    "Washington": "华盛顿", "New York": "纽约", "Norfolk": "诺福克", "San Diego": "圣迭戈",
    "Pearl Harbor": "珍珠港", "Seattle": "西雅图", "Bremerton": "布雷默顿", "Mayport": "梅波特",
    "Kings Bay": "金斯湾", "Groton": "格罗顿", "Newport": "纽波特", "Coronado": "科罗纳多",
    "Ottawa": "渥太华", "Halifax": "哈利法克斯", "Esquimalt": "埃斯奎莫尔特",
}

# 2. Syllable Transliteration Engine (Xinhua standard phonetics for foreign names)
PHONETIC_MAP = [
    (r"\bNew\s+", "新"), (r"\bNorth\s+", "北"), (r"\bSouth\s+", "南"),
    (r"\bEast\s+", "东"), (r"\bWest\s+", "西"), (r"\bPort\s+", "港"),
    (r"\bFort\s+", "堡"), (r"\bSaint\s+", "圣"), (r"\bSan\s+", "圣"),
    (r"\bSanta\s+", "圣"), (r"\bAl-", "阿尔"), (r"\bEl-", "埃尔"),
    (r"\bAl\s+", "阿尔"), (r"\bEl\s+", "埃尔"),
]

SYLLABLES = [
    ("ville", "维尔"), ("burgh", "堡"), ("burg", "堡"), ("ton", "顿"),
    ("ford", "福德"), ("field", "菲尔德"), ("land", "兰"), ("wood", "伍德"),
    ("bridge", "布里奇"), ("dale", "代尔"), ("port", "波特"), ("mouth", "茅斯"),
    ("shire", "夏尔"), ("chester", "切斯特"), ("wick", "威克"), ("grad", "格勒"),
    ("sk", "斯克"), ("berg", "贝格"), ("abad", "阿巴德"), ("pur", "普尔"),
    ("stan", "斯坦"), ("nagar", "纳加尔"), ("ganj", "甘杰"),
    ("ba", "巴"), ("pa", "帕"), ("ma", "马"), ("fa", "法"), ("da", "达"),
    ("ta", "塔"), ("na", "纳"), ("la", "拉"), ("ga", "加"), ("ka", "卡"),
    ("ha", "哈"), ("za", "扎"), ("sa", "萨"), ("sha", "沙"), ("cha", "查"),
    ("bi", "比"), ("pi", "皮"), ("mi", "米"), ("fi", "菲"), ("di", "迪"),
    ("ti", "蒂"), ("ni", "尼"), ("li", "利"), ("gi", "吉"), ("ki", "基"),
    ("hi", "希"), ("zi", "齐"), ("si", "斯"), ("shi", "希"), ("chi", "奇"),
    ("bu", "布"), ("pu", "普"), ("mu", "穆"), ("fu", "富"), ("du", "杜"),
    ("tu", "图"), ("nu", "努"), ("lu", "卢"), ("gu", "古"), ("ku", "库"),
    ("hu", "胡"), ("zu", "祖"), ("su", "苏"), ("shu", "舒"), ("chu", "楚"),
    ("be", "贝"), ("pe", "佩"), ("me", "梅"), ("fe", "费"), ("de", "德"),
    ("te", "特"), ("ne", "内"), ("le", "勒"), ("ge", "格"), ("ke", "克"),
    ("he", "赫"), ("ze", "泽"), ("se", "塞"), ("she", "舍"), ("che", "切"),
    ("bo", "博"), ("po", "珀"), ("mo", "莫"), ("fo", "福"), ("do", "多"),
    ("to", "托"), ("no", "诺"), ("lo", "洛"), ("go", "戈"), ("ko", "科"),
    ("ho", "霍"), ("zo", "佐"), ("so", "索"), ("sho", "绍"), ("cho", "乔"),
    ("an", "安"), ("en", "恩"), ("in", "因"), ("on", "昂"), ("un", "温"),
    ("ar", "阿尔"), ("er", "尔"), ("ir", "伊尔"), ("or", "奥尔"), ("ur", "乌尔"),
    ("al", "阿尔"), ("el", "埃尔"), ("il", "伊尔"), ("ol", "奥尔"), ("ul", "乌尔"),
    ("ro", "罗"), ("ra", "拉"), ("ri", "里"), ("ru", "鲁"), ("re", "雷"),
    ("ya", "亚"), ("yu", "尤"), ("yo", "约"), ("ye", "叶"),
    ("wa", "瓦"), ("wo", "沃"), ("we", "韦"),
    ("b", "布"), ("p", "普"), ("m", "姆"), ("f", "夫"), ("d", "德"),
    ("t", "特"), ("n", "恩"), ("l", "尔"), ("g", "格"), ("k", "克"),
    ("h", "赫"), ("z", "兹"), ("s", "斯"), ("r", "尔"),
    ("a", "阿"), ("e", "埃"), ("i", "伊"), ("o", "奥"), ("u", "乌"),
]


def transliterate(name: str) -> str:
    orig = name.strip()
    prefix = ""
    for pat, rep in PHONETIC_MAP:
        m = re.match(pat, name, re.I)
        if m:
            prefix += rep
            name = name[m.end():].strip()
            break

    s = name.lower()
    res = []
    i = 0
    while i < len(s):
        if not s[i].isalpha():
            if s[i] in " -/'’":
                if res and res[-1] != "·":
                    res.append("·")
            i += 1
            continue
        matched = False
        for syl, zh in SYLLABLES:
            if s.startswith(syl, i):
                res.append(zh)
                i += len(syl)
                matched = True
                break
        if not matched:
            i += 1

    out = prefix + "".join(res).strip("·")
    return out if out else orig


def build_cities_map() -> dict[str, str]:
    cities_file = GAME / "WW/PlacenameSets/Cities/0000_0000.wwp"
    bak = Path(str(cities_file) + ".orig")
    src_file = bak if bak.exists() else cities_file
    items = load(src_file)
    mapping: dict[str, str] = {}

    for it in items:
        name = it["name"]
        if name in MAJOR_CITIES:
            mapping[name] = MAJOR_CITIES[name]
        else:
            mapping[name] = transliterate(name)

    return mapping


def main() -> int:
    mapping = build_cities_map()
    dest = ROOT / "dict/placenames_cities.json"
    dest.write_text(json.dumps(mapping, ensure_ascii=False, indent=1),
                    encoding="utf-8")
    print(f"Built {dest}: {len(mapping)} cities mapped")
    cjk = sum(1 for v in mapping.values() if re.search(r"[\u4e00-\u9fff]", v))
    print(f"  CJK coverage: {cjk}/{len(mapping)} ({cjk*100.0/len(mapping):.1f}%)")
    sample = ["Nicosia", "Limassol", "Latakia", "Damascus", "Tel Aviv",
              "Beijing", "Shanghai", "Taipei", "Tokyo", "London", "Paris",
              "Zunyi", "Qiqihar", "Urumqi", "UEruemqi", "Shijiazhuang"]
    for s in sample:
        print(f"    {s!r:<16} -> {mapping.get(s)!r}")

    # Apply to the game file
    cities_file = GAME / "WW/PlacenameSets/Cities/0000_0000.wwp"
    bak = Path(str(cities_file) + ".orig")
    if not bak.exists():
        import shutil
        shutil.copy(cities_file, bak)
        print(f"Backed up {bak.name}")

    items = load(bak)
    renamed = 0
    for it in items:
        new = mapping.get(it["name"])
        if new and new != it["name"]:
            it["name"] = new
            renamed += 1
    save(cities_file, items)
    print(f"Applied to {cities_file}: {renamed}/{len(items)} cities renamed to Chinese!")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
