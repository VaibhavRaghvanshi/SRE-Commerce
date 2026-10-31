import { CommerceQuote } from '../services/commerce-pricing.service';
export interface CommerceCartLine {productId:string;variantId:string;sku:string;name:string;slug:string;quantity:number;}
export interface CommerceCart {id:string;tenantId:string;guest:boolean;status:number;revision:number;expiresAt:string;items:CommerceCartLine[];version:string;}
export interface CommerceCartPreview {cart:CommerceCart;quote:CommerceQuote;stock:{variantId:string;available:number;tracked:boolean;lowStock:boolean}[];canPrepare:boolean;}
export interface CommercePreparation {id:string;cartId:string;tenantId:string;userId:string|null;cartRevision:number;expiresAt:string;items:CommerceCartLine[];quote:CommerceQuote;}
