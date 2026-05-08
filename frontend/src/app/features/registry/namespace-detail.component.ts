import { CommonModule } from '@angular/common';
import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import {
  PackageSummaryDto,
  RegistryApiService,
  SkillVersionDto,
  registryApiErrorMessage,
} from '../../core/services/registry-api.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-namespace-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './namespace-detail.component.html',
  styleUrl: './namespace-detail.component.css',
})
export class NamespaceDetailComponent implements OnInit, OnDestroy {
  private readonly api = inject(RegistryApiService);
  private readonly route = inject(ActivatedRoute);
  private sub?: Subscription;

  readonly nsSlug = signal('');
  readonly loading = signal(true);
  readonly pageError = signal<string | null>(null);
  readonly packages = signal<PackageSummaryDto[]>([]);

  readonly pkgBusy = signal(false);
  readonly pkgErr = signal<string | null>(null);
  pkgSlug = '';
  pkgTitle = '';
  pkgDescription = '';

  expanded: Record<string, boolean> = {};
  versionsBySkill: Record<string, SkillVersionDto[]> = {};
  versionsLoading: Record<string, boolean> = {};
  versionsErr: Record<string, string | undefined> = {};

  publishDraft: Record<string, { version: string; tag: string; artifactUri: string }> = {};
  /** Stored ZIP per skill when publishing via upload (not bound to ngModel). */
  publishZipFileBySkill: Record<string, File | undefined> = {};
  publishSourceBySkill: Record<string, 'zip' | 'uri'> = {};
  publishBusy: Record<string, boolean> = {};
  publishErr: Record<string, string | undefined> = {};

  readonly skillMdLoading = signal(false);
  readonly skillMdErr = signal<string | null>(null);
  readonly skillMdView = signal<{ skillSlug: string; version: string; body: string } | null>(null);

  ngOnInit(): void {
    this.sub = this.route.paramMap.subscribe((pm) => {
      const slug = pm.get('slug');
      if (!slug) {
        return;
      }
      this.nsSlug.set(slug);
      this.reloadPackages();
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private ensurePublishDraft(skillSlug: string): void {
    if (!this.publishDraft[skillSlug]) {
      this.publishDraft[skillSlug] = {
        version: '1.0.0',
        tag: '',
        artifactUri: `file://skills/${skillSlug}/`,
      };
    }
  }

  private primeDrafts(pkgs: PackageSummaryDto[]): void {
    for (const p of pkgs) {
      this.ensurePublishDraft(p.slug);
      if (!this.publishSourceBySkill[p.slug]) {
        this.publishSourceBySkill[p.slug] = 'zip';
      }
    }
  }

  publishSource(skillSlug: string): 'zip' | 'uri' {
    return this.publishSourceBySkill[skillSlug] ?? 'zip';
  }

  setPublishSource(skillSlug: string, mode: 'zip' | 'uri'): void {
    this.publishSourceBySkill[skillSlug] = mode;
    this.publishErr[skillSlug] = undefined;
  }

  pickPublishZip(skillSlug: string, ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const f = input.files?.[0];
    this.publishZipFileBySkill[skillSlug] = f ?? undefined;
    this.publishErr[skillSlug] = undefined;
  }

  zipLabel(skillSlug: string): string {
    const f = this.publishZipFileBySkill[skillSlug];
    return f ? f.name : 'No file chosen';
  }

  private clearZipInput(skillSlug: string): void {
    const el = document.querySelector(
      `[data-skill-zip="${CSS.escape(skillSlug)}"]`,
    ) as HTMLInputElement | null;
    if (el) {
      el.value = '';
    }
    delete this.publishZipFileBySkill[skillSlug];
  }

  skillMdOpen(): boolean {
    return this.skillMdLoading() || this.skillMdErr() !== null || this.skillMdView() !== null;
  }

  openSkillInManager(skillSlug: string, version: string): void {
    const ns = this.nsSlug();
    if (!ns) {
      return;
    }
    this.skillMdView.set(null);
    this.skillMdErr.set(null);
    this.skillMdLoading.set(true);
    this.api.getSkillMarkdown(ns, skillSlug, version).subscribe({
      next: (body) => {
        this.skillMdLoading.set(false);
        this.skillMdView.set({ skillSlug, version, body });
      },
      error: (err) => {
        this.skillMdLoading.set(false);
        this.skillMdErr.set(registryApiErrorMessage(err));
      },
    });
  }

  closeSkillMd(): void {
    this.skillMdLoading.set(false);
    this.skillMdErr.set(null);
    this.skillMdView.set(null);
  }

  /** One-liner assuming `andy-skill` is on PATH (`npm install -g ./cli` from repo). */
  cliInstallGlobal(skillSlug: string, version: string): string {
    const ns = this.nsSlug();
    const r = environment.cliRegistryUrl.replace(/\/+$/, '');
    return `andy-skill install --registry ${r} ${ns} ${skillSlug} ${version}`;
  }

  /** From cloned repo root; installs CLI deps then extracts into ~/.agents/skills by default. */
  cliInstallFromRepo(skillSlug: string, version: string): string {
    const ns = this.nsSlug();
    const r = environment.cliRegistryUrl.replace(/\/+$/, '');
    return `cd cli && npm install && node bin/andy-skill.js install --registry ${r} ${ns}/${skillSlug}@${version}`;
  }

  async copyText(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      /* ignore */
    }
  }

  async copySkillMd(body: string): Promise<void> {
    await this.copyText(body);
  }

  reloadPackages(): void {
    const slug = this.nsSlug();
    if (!slug) {
      return;
    }
    this.loading.set(true);
    this.pageError.set(null);
    this.api.listPackages(slug).subscribe({
      next: (pkgs) => {
        this.packages.set(pkgs);
        this.primeDrafts(pkgs);
        this.loading.set(false);
      },
      error: (err) => {
        this.pageError.set(registryApiErrorMessage(err));
        this.loading.set(false);
      },
    });
  }

  submitPackage(): void {
    const slug = this.nsSlug();
    this.pkgErr.set(null);
    const s = this.pkgSlug.trim();
    const t = this.pkgTitle.trim();
    if (!slug || !s || !t) {
      this.pkgErr.set('Skill slug and title are required.');
      return;
    }
    this.pkgBusy.set(true);
    this.api
      .createPackage(slug, {
        slug: s,
        title: t,
        description: this.pkgDescription.trim() || null,
      })
      .subscribe({
        next: () => {
          this.pkgBusy.set(false);
          this.pkgSlug = '';
          this.pkgTitle = '';
          this.pkgDescription = '';
          this.reloadPackages();
        },
        error: (err) => {
          this.pkgBusy.set(false);
          this.pkgErr.set(registryApiErrorMessage(err));
        },
      });
  }

  toggleVersions(skillSlug: string): void {
    this.ensurePublishDraft(skillSlug);
    const open = !this.expanded[skillSlug];
    this.expanded[skillSlug] = open;
    if (open && !this.versionsBySkill[skillSlug]?.length && !this.versionsLoading[skillSlug]) {
      this.loadVersions(skillSlug);
    }
  }

  isExpanded(skillSlug: string): boolean {
    return !!this.expanded[skillSlug];
  }

  loadVersions(skillSlug: string): void {
    const ns = this.nsSlug();
    if (!ns) {
      return;
    }
    this.versionsErr[skillSlug] = undefined;
    this.versionsLoading[skillSlug] = true;
    this.api.listVersions(ns, skillSlug).subscribe({
      next: (rows) => {
        this.versionsBySkill[skillSlug] = rows;
        this.versionsLoading[skillSlug] = false;
      },
      error: (err) => {
        this.versionsLoading[skillSlug] = false;
        this.versionsErr[skillSlug] = registryApiErrorMessage(err);
      },
    });
  }

  versionsFor(skillSlug: string): SkillVersionDto[] {
    return this.versionsBySkill[skillSlug] ?? [];
  }

  getDraft(skillSlug: string): { version: string; tag: string; artifactUri: string } {
    this.ensurePublishDraft(skillSlug);
    return this.publishDraft[skillSlug];
  }

  publishVersion(skillSlug: string): void {
    const ns = this.nsSlug();
    const d = this.publishDraft[skillSlug];
    if (!ns || !d) {
      return;
    }
    this.publishErr[skillSlug] = undefined;
    const version = d.version.trim();
    const uri = d.artifactUri.trim();
    if (!version || !uri) {
      this.publishErr[skillSlug] = 'Version and artifact URI are required.';
      return;
    }
    this.publishBusy[skillSlug] = true;
    this.api
      .publishVersion(ns, skillSlug, {
        version,
        tag: d.tag.trim() || null,
        artifactUri: uri,
      })
      .subscribe({
        next: () => {
          this.publishBusy[skillSlug] = false;
          if (this.expanded[skillSlug]) {
            this.loadVersions(skillSlug);
          }
          this.reloadPackages();
        },
        error: (err) => {
          this.publishBusy[skillSlug] = false;
          this.publishErr[skillSlug] = registryApiErrorMessage(err);
        },
      });
  }

  submitPublishZip(skillSlug: string): void {
    const ns = this.nsSlug();
    const d = this.publishDraft[skillSlug];
    const file = this.publishZipFileBySkill[skillSlug];
    if (!ns || !d) {
      return;
    }
    this.publishErr[skillSlug] = undefined;
    const version = d.version.trim();
    if (!version) {
      this.publishErr[skillSlug] = 'Version is required.';
      return;
    }
    if (!file) {
      this.publishErr[skillSlug] = 'Choose a .zip file.';
      return;
    }
    this.publishBusy[skillSlug] = true;
    this.api
      .publishVersionFromZip(ns, skillSlug, {
        version,
        tag: d.tag.trim() || null,
        file,
      })
      .subscribe({
        next: () => {
          this.publishBusy[skillSlug] = false;
          this.clearZipInput(skillSlug);
          if (this.expanded[skillSlug]) {
            this.loadVersions(skillSlug);
          }
          this.reloadPackages();
        },
        error: (err) => {
          this.publishBusy[skillSlug] = false;
          this.publishErr[skillSlug] = registryApiErrorMessage(err);
        },
      });
  }
}
