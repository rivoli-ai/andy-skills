import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  catchError,
  finalize,
  map,
  of,
  type Observable,
  Subscription,
  switchMap,
} from 'rxjs';
import {
  PackageSummaryDto,
  RegistryApiService,
  SkillVersionDto,
  registryApiErrorMessage,
} from '../../core/services/registry-api.service';
import { APP_CONFIG } from '../../core/services/config.service';
import { CodeHighlightPipe } from '../../shared/pipes/code-highlight.pipe';
import { SkillMarkdownPipe } from '../../shared/pipes/skill-markdown.pipe';
import { CustomSelectComponent } from '../../shared/components/custom-select/custom-select.component';

type LoadResult =
  | { kind: 'ok'; pkg: PackageSummaryDto; versions: SkillVersionDto[] }
  | { kind: 'notfound' }
  | { kind: 'err'; message: string };

type SkillDetailTab = 'readme' | 'overview' | 'versions';

type TreeNode = {
  name: string;
  fullPath: string;
  kind: 'dir' | 'file';
  children: TreeNode[];
};

type OverviewRow = {
  depth: number;
  name: string;
  path: string;
  kind: 'dir' | 'file';
};

type CliInstallScenario = {
  label: string;
  platform: string;
  target: string;
  command: string;
};

function buildSkillZipTree(paths: string[]): TreeNode {
  const root: TreeNode = { name: '', fullPath: '', kind: 'dir', children: [] };
  for (const p of paths) {
    const parts = p.split('/').filter((x) => x.length > 0);
    if (parts.length === 0) continue;
    let cur = root;
    for (let i = 0; i < parts.length; i++) {
      const part = parts[i];
      const isFile = i === parts.length - 1;
      const segmentPath = parts.slice(0, i + 1).join('/');
      let child = cur.children.find((c) => c.name === part);
      if (!child) {
        child = {
          name: part,
          fullPath: isFile ? p : segmentPath,
          kind: isFile ? 'file' : 'dir',
          children: [],
        };
        cur.children.push(child);
      }
      cur = child;
    }
  }
  sortSkillTree(root);
  return root;
}

function sortSkillTree(node: TreeNode): void {
  node.children.sort((a, b) => {
    if (a.kind !== b.kind) return a.kind === 'file' ? 1 : -1;
    return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  });
  for (const c of node.children) {
    if (c.kind === 'dir') sortSkillTree(c);
  }
}

function flattenOverviewRows(root: TreeNode, collapsed: Record<string, boolean>): OverviewRow[] {
  const rows: OverviewRow[] = [];
  const walk = (node: TreeNode, depth: number) => {
    for (const c of node.children) {
      if (c.kind === 'file') {
        rows.push({ depth, name: c.name, path: c.fullPath, kind: 'file' });
      } else {
        rows.push({ depth, name: c.name, path: c.fullPath, kind: 'dir' });
        if (!collapsed[c.fullPath]) walk(c, depth + 1);
      }
    }
  };
  walk(root, 0);
  return rows;
}

@Component({
  selector: 'app-skill-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CodeHighlightPipe, SkillMarkdownPipe, CustomSelectComponent],
  templateUrl: './skill-detail.component.html',
  styleUrl: './skill-detail.component.css',
})
export class SkillDetailComponent implements OnInit, OnDestroy {
  private readonly api = inject(RegistryApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly appConfig = inject(APP_CONFIG);
  private sub?: Subscription;

  @ViewChild('publishZipInput') publishZipInput?: ElementRef<HTMLInputElement>;

  /** Highlight ZIP drop zone while dragging files over it. */
  readonly publishZipDragging = signal(false);

  readonly nsSlug = signal('');
  readonly skillSlug = signal('');
  /** Set when arriving from `/ns/:slug` via `?from=ns`. */
  readonly enteredFromNamespaceDetail = signal(false);
  readonly loading = signal(true);
  readonly pageError = signal<string | null>(null);
  readonly pkg = signal<PackageSummaryDto | null>(null);
  readonly versions = signal<SkillVersionDto[]>([]);

  readonly activeTab = signal<SkillDetailTab>('readme');
  /** Version string for install commands on the Readme tab */
  readonly readmeVersion = signal('');
  /** Version whose ZIP powers the Overview explorer */
  readonly overviewVersion = signal('');

  readonly zipPaths = signal<string[]>([]);
  readonly overviewTreeLoading = signal(false);
  readonly overviewTreeErr = signal<string | null>(null);
  /** Keys are directory paths; value true means collapsed. */
  readonly overviewCollapsed = signal<Record<string, boolean>>({});

  readonly overviewTreeRoot = computed(() => buildSkillZipTree(this.zipPaths()));
  readonly overviewRows = computed(() =>
    flattenOverviewRows(this.overviewTreeRoot(), this.overviewCollapsed()),
  );

  readonly overviewSelectedPath = signal<string | null>(null);
  readonly overviewFileLoading = signal(false);
  readonly overviewFileErr = signal<string | null>(null);
  readonly overviewFileContent = signal<string | null>(null);
  readonly overviewFileBinary = signal(false);
  readonly overviewFileTruncated = signal(false);
  readonly overviewFileBytes = signal(0);

  readonly overviewFileLines = computed(() => {
    const c = this.overviewFileContent();
    if (c === null) return [];
    return c.split(/\r?\n/);
  });

  readonly overviewMobilePane = signal<'explorer' | 'code'>('explorer');

  readonly readmeVersionSelectOptions = computed(() =>
    this.versions().map((v) => ({
      value: v.version,
      label:
        v.version +
        (v.isLatest ? ' (latest)' : '') +
        (!v.hasStoredZip ? ' — remote URI' : ''),
    })),
  );

  readonly overviewPackageSelectOptions = computed(() =>
    this.versionsWithZip().map((v) => ({
      value: v.version,
      label: v.version + (v.isLatest ? ' (latest)' : ''),
    })),
  );

  readonly skillBackRouterLink = computed(() =>
    this.enteredFromNamespaceDetail() ? ['/ns', this.nsSlug()] : '/skills',
  );

  readonly skillBackLabel = computed(() =>
    this.enteredFromNamespaceDetail() ? '← Namespace' : '← All skills',
  );

  publishDraft: { version: string; tag: string; artifactUri: string; publisherPatOneTime: string } = {
    version: '1.0.0',
    tag: '',
    artifactUri: '',
    publisherPatOneTime: '',
  };
  publishSourceMode: 'zip' | 'uri' = 'zip';
  publishZipFile?: File;
  readonly publishBusy = signal(false);
  readonly publishVersionErr = signal<string | null>(null);
  readonly publishZipErr = signal<string | null>(null);
  readonly publishUriErr = signal<string | null>(null);
  /** Versions tab: show publish form only after clicking "Publish a new version". */
  readonly publishFormOpen = signal(false);

  private clearPublishFieldErrors(): void {
    this.publishVersionErr.set(null);
    this.publishZipErr.set(null);
    this.publishUriErr.set(null);
  }

  onPublishVersionFieldChange(): void {
    if (this.publishVersionErr()) {
      this.publishVersionErr.set(null);
    }
  }

  onPublishUriFieldChange(): void {
    if (this.publishUriErr()) {
      this.publishUriErr.set(null);
    }
  }

  ngOnInit(): void {
    this.sub = new Subscription();
    this.sub.add(
      this.route.queryParamMap.subscribe((qm) => {
        this.enteredFromNamespaceDetail.set(qm.get('from') === 'ns');
      }),
    );
    this.sub.add(
      this.route.paramMap
        .pipe(
          switchMap((pm) => {
            const ns = pm.get('namespaceSlug');
            const skill = pm.get('skillSlug');
            if (!ns || !skill) {
              this.loading.set(false);
              this.pkg.set(null);
              this.versions.set([]);
              return of<LoadResult>({ kind: 'err', message: 'Invalid skill URL.' });
            }
            this.nsSlug.set(ns);
            this.skillSlug.set(skill);
            this.loading.set(true);
            this.pageError.set(null);
            this.pkg.set(null);
            this.versions.set([]);
            return this.loadSkill(ns, skill).pipe(finalize(() => this.loading.set(false)));
          }),
        )
        .subscribe((res) => this.applyLoadResult(res)),
    );
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  versionsWithZip(): SkillVersionDto[] {
    return this.versions().filter((v) => v.hasStoredZip);
  }

  versionRow(version: string): SkillVersionDto | undefined {
    return this.versions().find((v) => v.version === version);
  }

  pickDefaultVersionForPickers(vers: SkillVersionDto[]): string {
    const zipVers = vers.filter((v) => v.hasStoredZip);
    const byLatest =
      zipVers.find((v) => v.isLatest)?.version ?? zipVers[0]?.version ?? '';
    if (byLatest) return byLatest;
    return vers.find((v) => v.isLatest)?.version ?? vers[0]?.version ?? '';
  }

  private loadSkill(ns: string, skill: string): Observable<LoadResult> {
    return this.api.listPackages(ns).pipe(
      switchMap((pkgs): Observable<LoadResult> => {
        const pkgRow = pkgs.find((p) => p.slug === skill);
        if (!pkgRow) {
          return of<LoadResult>({ kind: 'notfound' });
        }
        return this.api.listVersions(ns, skill).pipe(
          map((rows): LoadResult => ({ kind: 'ok', pkg: pkgRow, versions: rows })),
        );
      }),
      catchError((err): Observable<LoadResult> =>
        of({ kind: 'err', message: registryApiErrorMessage(err) }),
      ),
    );
  }

  private applyLoadResult(res: LoadResult): void {
    if (res.kind === 'err') {
      this.pageError.set(res.message);
      this.pkg.set(null);
      this.versions.set([]);
      return;
    }
    if (res.kind === 'notfound') {
      this.pageError.set('This skill does not exist in this namespace.');
      this.pkg.set(null);
      this.versions.set([]);
      return;
    }
    this.pkg.set(res.pkg);
    this.versions.set(res.versions);
    const pick = this.pickDefaultVersionForPickers(res.versions);
    this.readmeVersion.set(pick);
    this.overviewVersion.set(pick);
    this.resetPublishDraft();
    this.clearOverviewExplorerState();
    this.applyTabFromQuery();
    if (this.activeTab() === 'overview') {
      this.loadOverviewTree();
    }
  }

  private applyTabFromQuery(): void {
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab === 'readme' || tab === 'overview' || tab === 'versions') {
      this.activeTab.set(tab);
      if (tab !== 'versions') {
        this.publishFormOpen.set(false);
      }
    } else {
      this.activeTab.set('readme');
      this.publishFormOpen.set(false);
    }
  }

  private clearOverviewExplorerState(): void {
    this.zipPaths.set([]);
    this.overviewTreeErr.set(null);
    this.overviewCollapsed.set({});
    this.overviewSelectedPath.set(null);
    this.overviewFileContent.set(null);
    this.overviewFileErr.set(null);
    this.overviewFileBinary.set(false);
    this.overviewFileTruncated.set(false);
    this.overviewFileBytes.set(0);
  }

  setTab(tab: SkillDetailTab): void {
    this.activeTab.set(tab);
    if (tab !== 'versions') {
      this.publishFormOpen.set(false);
    }
    if (tab === 'overview') {
      this.loadOverviewTree();
    }
  }

  openPublishForm(): void {
    this.clearPublishFieldErrors();
    this.publishFormOpen.set(true);
  }

  cancelPublishForm(): void {
    this.publishFormOpen.set(false);
    this.clearPublishFieldErrors();
  }

  onReadmeVersionChange(version: string): void {
    this.readmeVersion.set(version);
  }

  onOverviewVersionChange(version: string): void {
    this.overviewVersion.set(version);
    this.clearOverviewExplorerState();
    this.loadOverviewTree();
  }

  loadOverviewTree(): void {
    const ns = this.nsSlug();
    const slug = this.skillSlug();
    const ver = this.overviewVersion();
    const row = this.versionRow(ver);
    if (!ns || !slug || !ver || !row?.hasStoredZip) {
      this.zipPaths.set([]);
      this.overviewTreeLoading.set(false);
      if (ver && row && !row.hasStoredZip) {
        this.overviewTreeErr.set(
          'This version has no ZIP in the registry. Upload a ZIP version or pick another release.',
        );
      } else {
        this.overviewTreeErr.set(null);
      }
      return;
    }
    this.overviewTreeLoading.set(true);
    this.overviewTreeErr.set(null);
    this.api.getSkillZipTree(ns, slug, ver).subscribe({
      next: (dto) => {
        this.zipPaths.set(dto.paths);
        this.overviewCollapsed.set({});
        this.overviewTreeLoading.set(false);
        const sel = this.overviewSelectedPath();
        if (sel && !dto.paths.includes(sel)) {
          this.closeOverviewFile();
        }
      },
      error: (err) => {
        this.zipPaths.set([]);
        this.overviewTreeLoading.set(false);
        this.overviewTreeErr.set(registryApiErrorMessage(err));
      },
    });
  }

  toggleOverviewDir(dirPath: string): void {
    this.overviewCollapsed.update((m) => {
      const next = { ...m };
      if (next[dirPath]) delete next[dirPath];
      else next[dirPath] = true;
      return next;
    });
  }

  overviewDirExpanded(dirPath: string): boolean {
    return !this.overviewCollapsed()[dirPath];
  }

  selectOverviewFile(path: string): void {
    const ns = this.nsSlug();
    const slug = this.skillSlug();
    const ver = this.overviewVersion();
    if (!ns || !slug || !ver) return;
    this.overviewSelectedPath.set(path);
    this.overviewMobilePane.set('code');
    this.overviewFileLoading.set(true);
    this.overviewFileErr.set(null);
    this.overviewFileContent.set(null);
    this.overviewFileBinary.set(false);
    this.overviewFileTruncated.set(false);
    this.api.getSkillZipFile(ns, slug, ver, path).subscribe({
      next: (dto) => {
        this.overviewFileLoading.set(false);
        this.overviewFileBinary.set(dto.isBinary);
        this.overviewFileTruncated.set(!!dto.truncated);
        this.overviewFileBytes.set(dto.sizeBytes);
        this.overviewFileContent.set(dto.isBinary ? null : dto.content);
      },
      error: (err) => {
        this.overviewFileLoading.set(false);
        this.overviewFileErr.set(registryApiErrorMessage(err));
      },
    });
  }

  closeOverviewFile(): void {
    this.overviewSelectedPath.set(null);
    this.overviewFileContent.set(null);
    this.overviewFileErr.set(null);
    this.overviewFileBinary.set(false);
    this.overviewFileTruncated.set(false);
    this.overviewFileBytes.set(0);
    this.overviewFileLoading.set(false);
  }

  overviewRowSelected(path: string): boolean {
    return this.overviewSelectedPath() === path;
  }

  openOverviewForVersion(version: string, preferSkillMd = false): void {
    this.overviewVersion.set(version);
    this.activeTab.set('overview');
    this.clearOverviewExplorerState();
    const ns = this.nsSlug();
    const slug = this.skillSlug();
    const row = this.versionRow(version);
    if (!ns || !slug || !row?.hasStoredZip) {
      this.overviewTreeErr.set(
        row && !row.hasStoredZip
          ? 'This version has no ZIP in the registry.'
          : 'Pick a version with a stored ZIP.',
      );
      return;
    }
    this.overviewTreeLoading.set(true);
    this.overviewTreeErr.set(null);
    this.api.getSkillZipTree(ns, slug, version).subscribe({
      next: (dto) => {
        this.zipPaths.set(dto.paths);
        this.overviewCollapsed.set({});
        this.overviewTreeLoading.set(false);
        let pick: string | undefined;
        if (preferSkillMd) {
          pick = dto.paths.find(
            (p) => p.split('/').pop()?.toLowerCase() === 'skill.md',
          );
        }
        pick ??= dto.paths[0];
        if (pick) this.selectOverviewFile(pick);
      },
      error: (err) => {
        this.zipPaths.set([]);
        this.overviewTreeLoading.set(false);
        this.overviewTreeErr.set(registryApiErrorMessage(err));
      },
    });
  }

  overviewLanguageLabel(path: string | null): string {
    if (!path) return 'text';
    const dot = path.lastIndexOf('.');
    if (dot < 0) return 'text';
    const ext = path.slice(dot + 1).toLowerCase();
    const map: Record<string, string> = {
      md: 'markdown',
      ts: 'typescript',
      tsx: 'tsx',
      js: 'javascript',
      jsx: 'jsx',
      mjs: 'javascript',
      cjs: 'javascript',
      json: 'json',
      html: 'html',
      css: 'css',
      scss: 'scss',
      py: 'python',
      cs: 'csharp',
      rs: 'rust',
      go: 'go',
      yaml: 'yaml',
      yml: 'yaml',
      sh: 'shell',
      xml: 'xml',
    };
    return map[ext] ?? ext;
  }

  overviewViewerIsMarkdown(): boolean {
    const path = this.overviewSelectedPath();
    if (!path) return false;
    const name = path.split('/').pop()?.toLowerCase() ?? '';
    return name.endsWith('.md');
  }

  /** Lang label passed to `codeHighlight` (same family as `overviewLanguageLabel`). */
  overviewPrismLanguage(): string {
    return this.overviewLanguageLabel(this.overviewSelectedPath());
  }

  /** Explorer icon tag — mirrors DevPilot <span class="node-icon" data-type>. */
  overviewExplorerIconType(fileName: string): string {
    const lower = fileName.toLowerCase();
    if (lower === 'dockerfile') return 'docker';
    if (lower === 'skill.md') return 'markdown';

    const ext = fileName.includes('.')
      ? (fileName.split('.').pop()?.toLowerCase() ?? '')
      : '';
    const iconMap: Record<string, string> = {
      ts: 'typescript',
      tsx: 'typescript',
      mts: 'typescript',
      cts: 'typescript',
      js: 'javascript',
      jsx: 'javascript',
      mjs: 'javascript',
      cjs: 'javascript',
      py: 'python',
      java: 'java',
      cs: 'csharp',
      go: 'go',
      rs: 'rust',
      rb: 'ruby',
      php: 'php',
      html: 'html',
      htm: 'html',
      css: 'css',
      scss: 'css',
      sass: 'css',
      json: 'json',
      xml: 'xml',
      yaml: 'yaml',
      yml: 'yaml',
      md: 'markdown',
      sql: 'database',
      sh: 'terminal',
      bash: 'terminal',
      zsh: 'terminal',
      png: 'image',
      jpg: 'image',
      jpeg: 'image',
      gif: 'image',
      svg: 'image',
      webp: 'image',
      pdf: 'pdf',
      zip: 'archive',
      tar: 'archive',
      gz: 'archive',
    };
    return iconMap[ext] || 'file';
  }

  formatOverviewSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  setOverviewMobilePane(pane: 'explorer' | 'code'): void {
    this.overviewMobilePane.set(pane);
  }

  private resetPublishDraft(): void {
    const slug = this.skillSlug();
    this.publishDraft = {
      version: '1.0.0',
      tag: '',
      artifactUri: slug ? `file://skills/${slug}/` : '',
      publisherPatOneTime: '',
    };
    this.publishSourceMode = 'zip';
    this.publishZipFile = undefined;
    this.clearPublishFieldErrors();
  }

  private refreshAfterPublish(): void {
    const ns = this.nsSlug();
    const skill = this.skillSlug();
    if (!ns || !skill) return;
    this.loadSkill(ns, skill).subscribe((res) => {
      if (res.kind !== 'ok') return;
      this.pkg.set(res.pkg);
      this.versions.set(res.versions);
      const pick = this.pickDefaultVersionForPickers(res.versions);
      this.readmeVersion.set(pick);
      this.overviewVersion.set(pick);
      this.resetPublishDraft();
      this.publishFormOpen.set(false);
      this.clearOverviewExplorerState();
      if (this.activeTab() === 'overview') this.loadOverviewTree();
    });
  }

  setPublishSource(mode: 'zip' | 'uri'): void {
    this.publishSourceMode = mode;
    this.clearPublishFieldErrors();
  }

  pickPublishZip(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const f = input.files?.[0];
    this.publishZipFile = f ?? undefined;
    this.publishZipErr.set(null);
  }

  publishZipPrimaryLine(): string {
    return this.publishZipFile ? this.publishZipFile.name : 'Drop skill package (.zip) here';
  }

  publishZipMetaLine(): string {
    if (this.publishZipFile) {
      return `${this.formatOverviewSize(this.publishZipFile.size)} · ready to publish`;
    }
    return 'or click to browse · ZIP must include SKILL.md at the root';
  }

  onPublishZipDragEnter(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.publishZipDragging.set(true);
  }

  onPublishZipDragLeave(ev: DragEvent): void {
    ev.preventDefault();
    const wrap = ev.currentTarget as HTMLElement;
    const related = ev.relatedTarget as Node | null;
    if (related && wrap.contains(related)) {
      return;
    }
    this.publishZipDragging.set(false);
  }

  onPublishZipDragOver(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
  }

  onPublishZipDrop(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.publishZipDragging.set(false);
    const f = ev.dataTransfer?.files?.[0];
    if (!f) {
      return;
    }
    const okZip =
      f.name.toLowerCase().endsWith('.zip') ||
      f.type === 'application/zip' ||
      f.type === 'application/x-zip-compressed';
    if (!okZip) {
      this.publishZipErr.set('Please choose or drop a .zip file.');
      return;
    }
    this.publishZipFile = f;
    this.publishZipErr.set(null);
  }

  clearPublishZip(ev: Event): void {
    ev.preventDefault();
    ev.stopPropagation();
    const input = this.publishZipInput?.nativeElement;
    if (input) {
      input.value = '';
    }
    this.publishZipFile = undefined;
    this.publishZipErr.set(null);
  }

  private clearZipInput(): void {
    const input = this.publishZipInput?.nativeElement;
    if (input) {
      input.value = '';
    }
    this.publishZipFile = undefined;
  }

  publishVersion(): void {
    const ns = this.nsSlug();
    const slug = this.skillSlug();
    const d = this.publishDraft;
    if (!ns || !slug) return;
    this.clearPublishFieldErrors();
    const version = d.version.trim();
    const uri = d.artifactUri.trim();
    let invalid = false;
    if (!version) {
      this.publishVersionErr.set('Version is required.');
      invalid = true;
    }
    if (!uri) {
      this.publishUriErr.set('Artifact URI is required.');
      invalid = true;
    }
    if (invalid) {
      return;
    }
    this.publishBusy.set(true);
    this.api
      .publishVersion(ns, slug, {
        version,
        tag: d.tag.trim() || null,
        artifactUri: uri,
        publisherPatOneTime: d.publisherPatOneTime.trim() || null,
      })
      .subscribe({
        next: () => {
          this.publishBusy.set(false);
          this.refreshAfterPublish();
        },
        error: (err) => {
          this.publishBusy.set(false);
          this.publishRemoteVersionOrUriFailure(err);
        },
      });
  }

  submitPublishZip(): void {
    const ns = this.nsSlug();
    const slug = this.skillSlug();
    const d = this.publishDraft;
    const file = this.publishZipFile;
    if (!ns || !slug) return;
    this.clearPublishFieldErrors();
    const version = d.version.trim();
    if (!version) {
      this.publishVersionErr.set('Version is required.');
      return;
    }
    if (!file) {
      this.publishZipErr.set('Choose a .zip file.');
      return;
    }
    this.publishBusy.set(true);
    this.api
      .publishVersionFromZip(ns, slug, {
        version,
        tag: d.tag.trim() || null,
        file,
      })
      .subscribe({
        next: () => {
          this.publishBusy.set(false);
          this.clearZipInput();
          this.refreshAfterPublish();
        },
        error: (err) => {
          this.publishBusy.set(false);
          this.publishZipUploadFailure(err);
        },
      });
  }

  /** Route publish failures: version/duplicate/conflict → version field; otherwise ZIP vs URI. */
  private publishZipUploadFailure(err: unknown): void {
    const msg = registryApiErrorMessage(err);
    if (this.publishFailureTargetsVersion(msg, err)) {
      this.publishVersionErr.set(msg);
      return;
    }
    this.publishZipErr.set(msg);
  }

  private publishRemoteVersionOrUriFailure(err: unknown): void {
    const msg = registryApiErrorMessage(err);
    if (this.publishFailureTargetsVersion(msg, err)) {
      this.publishVersionErr.set(msg);
      return;
    }
    this.publishUriErr.set(msg);
  }

  private publishFailureTargetsVersion(msg: string, err: unknown): boolean {
    if (err instanceof HttpErrorResponse && err.status === 409) {
      return true;
    }
    const m = msg.toLowerCase();
    if (m.includes('invalid version')) {
      return true;
    }
    if (
      m.includes('version') &&
      (m.includes('already') ||
        m.includes('exist') ||
        m.includes('duplicate') ||
        m.includes('conflict'))
    ) {
      return true;
    }
    return false;
  }

  cliInstallGlobal(version: string, dir?: string): string {
    const ns = this.nsSlug();
    const slug = this.skillSlug();
    const r = (this.appConfig.cliRegistryUrl ?? 'http://localhost:5289').replace(/\/+$/, '');
    const command = `andy-skill install --registry ${r} ${ns} ${slug} ${version}`;
    return dir ? `${command} --dir ${dir}` : command;
  }

  cliInstallScenarios(version: string): CliInstallScenario[] {
    return [
      {
        label: 'Default CLI location',
        platform: 'All platforms',
        target: '~/.agents/skills',
        command: this.cliInstallGlobal(version),
      },
      {
        label: 'Cline workspace skills',
        platform: 'All platforms',
        target: '.cline/skills',
        command: this.cliInstallGlobal(version, '.cline/skills'),
      },
      {
        label: 'Cline global skills',
        platform: 'macOS/Linux',
        target: '~/.cline/skills',
        command: this.cliInstallGlobal(version, '~/.cline/skills'),
      },
      {
        label: 'Cline global skills',
        platform: 'Windows PowerShell',
        target: '$env:USERPROFILE\\.cline\\skills',
        command: this.cliInstallGlobal(version, '"$env:USERPROFILE\\.cline\\skills"'),
      },
    ];
  }

  async copyText(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      /* ignore */
    }
  }
}
