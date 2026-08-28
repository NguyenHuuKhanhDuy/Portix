import type { Dictionary } from './dictionary'

export const vi: Dictionary = {
  meta: {
    title: 'Portix — Tunnel tự lưu trữ cho ứng dụng local của bạn',
    description:
      'Portix giúp bạn công khai ứng dụng chạy trên máy local qua URL công khai, sử dụng chính tunnel server do bạn tự lưu trữ.',
  },
  themeToggle: {
    switchToLight: 'Chuyển sang giao diện sáng',
    switchToDark: 'Chuyển sang giao diện tối',
  },
  languageToggle: {
    switchToVietnamese: 'Chuyển sang tiếng Việt',
    switchToEnglish: 'Chuyển sang tiếng Anh',
  },
  hero: {
    badge: 'Tunnel tự lưu trữ',
    heading: 'Portix',
    subheading:
      'Công khai một cổng local qua URL công khai được phục vụ bởi tunnel server của riêng bạn, kèm công cụ theo dõi traffic trực tiếp.',
    downloadCta: 'Tải xuống',
    githubCta: 'Xem trên GitHub',
  },
  features: {
    heading: 'Mọi thứ bạn cần để công khai ứng dụng local',
    subheading: 'Quy trình CLI đơn giản để mở tunnel, chạy hoàn toàn trên hạ tầng do bạn kiểm soát.',
    items: [
      {
        title: 'Tự lưu trữ',
        description: 'Chạy tunnel server của riêng bạn trên domain và hạ tầng của chính bạn — không qua relay trung gian của bên thứ ba.',
      },
      {
        title: 'Theo dõi traffic trực tiếp',
        description: 'Xem từng request đi qua tunnel theo thời gian thực từ dashboard web local, không cần thiết lập thêm.',
      },
      {
        title: 'CLI quen thuộc',
        description: 'Lệnh `portix http <port>` đơn giản với bảng trạng thái trực tiếp cho từng tunnel đang mở.',
      },
      {
        title: 'Tunnel HTTP & HTTPS',
        description: 'Công khai ứng dụng local qua HTTP hoặc HTTPS thông qua tunnel, bao gồm cả nâng cấp WebSocket.',
      },
      {
        title: 'Quản lý tunnel đơn giản',
        description: 'Liệt kê và đóng các tunnel đang mở bất cứ lúc nào với `portix ls` và `portix rm`.',
      },
    ],
  },
  cliDemo: {
    heading: 'Một lệnh, traffic trực tiếp',
    subheading: 'Mở một tunnel và xem request đổ về ngay khi chúng xảy ra.',
    httpRequestsLabel: 'HTTP Requests',
    tableHeaders: {
      time: 'Thời gian',
      method: 'Method',
      path: 'Path',
      status: 'Trạng thái',
    },
  },
  architecture: {
    heading: 'Tunnel hoạt động như thế nào',
    subheading:
      'Client mở một kết nối điều khiển lâu dài đến Server và đăng ký một subdomain. Khi có request công khai đến, Server báo hiệu cho Client, Client sẽ mở một kết nối dữ liệu để chuyển tiếp byte theo cả hai chiều — bao gồm cả nâng cấp WebSocket.',
    flowDiagramAriaLabel:
      'Request đi từ ứng dụng của bạn qua Client và Server đến người truy cập công khai, rồi phản hồi quay ngược lại.',
    flowNodes: [
      { label: 'Ứng dụng của bạn' },
      { label: 'Client' },
      { label: 'Server' },
      { label: 'Người truy cập' },
    ],
    components: [
      {
        title: 'Server',
        description:
          'Relay hướng ra công khai. Nhận kết nối điều khiển từ client, định tuyến traffic công khai đến đúng tunnel theo subdomain, và quản lý người dùng cùng token.',
      },
      {
        title: 'Client',
        description:
          'CLI `portix` và daemon local. Đăng ký tunnel với Server, chuyển tiếp traffic đến ứng dụng local của bạn, và phục vụ dashboard theo dõi traffic.',
      },
      {
        title: 'Shared',
        description:
          'Mã giao thức truyền tải dùng chung bởi cả Server và Client — đóng gói message, kiểu dữ liệu, và xử lý header.',
      },
    ],
  },
  download: {
    heading: 'Tải Portix',
    subheading: 'Lấy bản build mới nhất cho nền tảng của bạn — giải nén và chạy, không cần cài đặt.',
    windowsTitle: 'Windows',
    windowsBit: '64-bit',
    macosTitle: 'macOS',
    macosPlatforms: 'Apple Silicon hoặc Intel',
    downloadLabel: 'Tải xuống',
    appleSiliconLabel: 'Apple Silicon',
    intelLabel: 'Intel',
    seeAllVersionsPrefix: 'Xem tất cả phiên bản trên',
    githubReleasesLabel: 'GitHub Releases',
    windowsInstructions: {
      summary: 'Hướng dẫn cài đặt trên Windows',
      step1Title: '1. Giải nén file thực thi',
      step1TextBefore: 'Giải nén file đã tải về vào một thư mục bạn sẽ giữ lại, ví dụ',
      step1TextAfter: '.',
      step2Title: '2. (Tùy chọn) Thêm vào PATH',
      step2TextBefore: 'Để bạn có thể chạy',
      step2TextAfter: 'từ bất kỳ terminal nào mà không cần gõ đường dẫn đầy đủ. Trong PowerShell:',
      step2ManualBefore: 'Hoặc thao tác thủ công: Settings → System → About → Advanced system settings → Environment Variables → chỉnh sửa',
      step2ManualMid: 'trong mục "User variables" → thêm thư mục',
      step2ManualAfter: 'vào đó. Khởi động lại terminal sau khi xong.',
      step3Title: '3. Chạy Portix',
      smartScreenTitle: 'Thông báo Windows SmartScreen',
      smartScreenIntro: 'Nếu bạn thấy "Windows protected your PC":',
      smartScreenStep1: 'Nhấp "More info"',
      smartScreenStep2: 'Nhấp "Run anyway"',
    },
    macInstructions: {
      summary: 'Hướng dẫn cài đặt trên macOS',
      step1Title: '1. Giải nén file thực thi',
      step1TextBefore: 'Giải nén file đã tải về vào',
      step2Title: '2. Cấp quyền thực thi',
      step2Text: 'Cấp quyền thực thi cho file:',
      step3Title: '3. Chạy Portix',
      securityTitle: 'Thông báo bảo mật macOS',
      securityIntro: 'Nếu bạn thấy thông báo "portix" was blocked to protect your Mac:',
      securityStep1: 'Vào System Settings → Privacy & Security',
      securityStep2: 'Cuộn xuống mục Security',
      securityStep3: 'Nhấp "Open Anyway"',
      securityStep4: 'Xác nhận để chạy ứng dụng',
    },
  },
  gettingStarted: {
    heading: 'Sẵn sàng công khai ứng dụng đầu tiên?',
    subheading: 'Ba bước để có một URL công khai trỏ đến thứ gì đó đang chạy trên máy của bạn.',
    steps: [
      {
        title: 'Mở terminal',
        description: 'Đi đến thư mục nơi bạn đã giải nén file tải về ở trên.',
      },
      {
        title: 'Đăng nhập',
        description: 'Dùng API token và địa chỉ server mà quản trị viên của bạn đã cung cấp.',
      },
      {
        title: 'Mở một tunnel',
        description: 'Công khai một cổng local và chia sẻ URL công khai mà nó in ra.',
      },
    ],
    cliReferenceSummary: 'Tra cứu CLI',
    cliCommands: [
      { description: 'Công khai một cổng HTTP local qua tunnel công khai.' },
      { description: 'Công khai một cổng HTTPS local qua tunnel công khai.' },
      { description: 'Liệt kê các tunnel đang mở.' },
      { description: 'Đóng một tunnel theo id.' },
      { description: 'Lưu API token cá nhân cho máy này.' },
      { description: 'Xóa API token đã lưu.' },
      { description: 'Hiển thị hướng dẫn sử dụng.' },
    ],
    readSetupGuideCta: 'Đọc hướng dẫn thiết lập đầy đủ',
  },
  footer: {
    tagline: 'Portix — một tunnel server tự lưu trữ.',
    madeBy: 'Được tạo bởi',
    githubLabel: 'GitHub',
  },
}
