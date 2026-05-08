import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG, registryApiPrefix } from './config.service';

export interface NamespaceDto {
  id: string;
  slug: string;
  displayName: string;
  description?: string | null;
  visibility: string;
  createdAtUtc: string;
  createdBySubject?: string | null;
}

export interface UpdateNamespaceBody {
  displayName: string;
  description?: string | null;
  visibility?: string | null;
}

export interface PackageSummaryDto {
  id: string;
  namespaceSlug: string;
  slug: string;
  title: string;
  description?: string | null;
  createdAtUtc: string;
  createdBySubject?: string | null;
  latestVersion?: string | null;
  hasLatest: boolean;
}

export interface SkillVersionDto {
  id: string;
  packageId: string;
  version: string;
  tag?: string | null;
  isLatest: boolean;
  artifactUri: string;
  publishedAtUtc: string;
  /** True when the registry holds the ZIP in Postgres (preview / install URL). */
  hasStoredZip: boolean;
}

export interface SkillZipTreeDto {
  paths: string[];
}

export interface SkillZipFileDto {
  path: string;
  content: string;
  isBinary: boolean;
  sizeBytes: number;
  truncated?: boolean;
}

export interface CreateNamespaceBody {
  slug: string;
  displayName: string;
  description?: string | null;
  visibility?: string | null;
}

export function registryApiErrorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error as { error?: string; message?: string; title?: string } | undefined;
    if (body?.error && typeof body.error === 'string') {
      return body.error;
    }
    if (body?.message && typeof body.message === 'string') {
      return body.message;
    }
    if (body?.title && typeof body.title === 'string') {
      return body.title;
    }
    if (err.status === 0) {
      return 'Network error — is the API reachable?';
    }
    return err.message || `HTTP ${err.status}`;
  }
  return 'Unexpected error';
}

@Injectable({ providedIn: 'root' })
export class RegistryApiService {
  private readonly http = inject(HttpClient);
  private readonly appConfig = inject(APP_CONFIG);
  private get base(): string {
    return registryApiPrefix(this.appConfig);
  }

  listNamespaces(): Observable<NamespaceDto[]> {
    return this.http.get<NamespaceDto[]>(`${this.base}/namespaces`);
  }

  createNamespace(body: CreateNamespaceBody): Observable<NamespaceDto> {
    return this.http.post<NamespaceDto>(`${this.base}/namespaces`, body);
  }

  updateNamespace(slug: string, body: UpdateNamespaceBody): Observable<NamespaceDto> {
    const s = encodeURIComponent(slug);
    return this.http.put<NamespaceDto>(`${this.base}/namespaces/${s}`, body);
  }

  deleteNamespace(slug: string): Observable<void> {
    const s = encodeURIComponent(slug);
    return this.http.delete<void>(`${this.base}/namespaces/${s}`);
  }

  listPackages(namespaceSlug: string): Observable<PackageSummaryDto[]> {
    return this.http.get<PackageSummaryDto[]>(
      `${this.base}/namespaces/${encodeURIComponent(namespaceSlug)}/packages`,
    );
  }

  createPackage(
    namespaceSlug: string,
    body: { slug: string; title: string; description?: string | null },
  ): Observable<PackageSummaryDto> {
    const ns = encodeURIComponent(namespaceSlug);
    return this.http.post<PackageSummaryDto>(`${this.base}/namespaces/${ns}/packages`, body);
  }

  deletePackage(namespaceSlug: string, skillSlug: string): Observable<void> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    return this.http.delete<void>(`${this.base}/namespaces/${ns}/packages/${sk}`);
  }

  updatePackage(
    namespaceSlug: string,
    skillSlug: string,
    body: { title: string; description?: string | null },
  ): Observable<PackageSummaryDto> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    return this.http.put<PackageSummaryDto>(
      `${this.base}/namespaces/${ns}/packages/${sk}`,
      body,
    );
  }

  listVersions(namespaceSlug: string, skillSlug: string): Observable<SkillVersionDto[]> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    return this.http.get<SkillVersionDto[]>(
      `${this.base}/namespaces/${ns}/packages/${sk}/versions`,
    );
  }

  publishVersion(
    namespaceSlug: string,
    skillSlug: string,
    body: {
      version: string;
      tag?: string | null;
      artifactUri: string;
      publisherPatOneTime?: string | null;
    },
  ): Observable<SkillVersionDto> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    return this.http.post<SkillVersionDto>(
      `${this.base}/namespaces/${ns}/packages/${sk}/versions`,
      body,
    );
  }

  getSkillMarkdown(namespaceSlug: string, skillSlug: string, version: string): Observable<string> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    const v = encodeURIComponent(version);
    return this.http.get(`${this.base}/namespaces/${ns}/packages/${sk}/versions/${v}/SKILL.md`, {
      responseType: 'text',
    });
  }

  getSkillZipTree(
    namespaceSlug: string,
    skillSlug: string,
    version: string,
  ): Observable<SkillZipTreeDto> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    const v = encodeURIComponent(version);
    return this.http.get<SkillZipTreeDto>(
      `${this.base}/namespaces/${ns}/packages/${sk}/versions/${v}/zip/tree`,
    );
  }

  getSkillZipFile(
    namespaceSlug: string,
    skillSlug: string,
    version: string,
    path: string,
  ): Observable<SkillZipFileDto> {
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    const v = encodeURIComponent(version);
    return this.http.get<SkillZipFileDto>(
      `${this.base}/namespaces/${ns}/packages/${sk}/versions/${v}/zip/file`,
      { params: { path } },
    );
  }

  /** Multipart upload; backend stores ZIP in Postgres and sets artifactUri to the install URL. */
  publishVersionFromZip(
    namespaceSlug: string,
    skillSlug: string,
    body: { version: string; tag?: string | null; file: File },
  ): Observable<SkillVersionDto> {
    const fd = new FormData();
    fd.append('version', body.version);
    const tag = body.tag?.trim();
    if (tag) {
      fd.append('tag', tag);
    }
    fd.append('file', body.file, body.file.name);
    const ns = encodeURIComponent(namespaceSlug);
    const sk = encodeURIComponent(skillSlug);
    return this.http.post<SkillVersionDto>(
      `${this.base}/namespaces/${ns}/packages/${sk}/versions/upload`,
      fd,
    );
  }
}
